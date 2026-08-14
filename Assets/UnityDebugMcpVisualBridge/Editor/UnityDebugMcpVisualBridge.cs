#if UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using GritGud.Presentation.Gameplay;

[InitializeOnLoad]
public static class UnityDebugMcpVisualBridge
{
    private const int Port = 57579;
    private const string MenuPath = "Tools/Unity Debug MCP/Start Visual Capture Bridge";
    private static readonly ConcurrentQueue<Action> MainThreadActions =
        new ConcurrentQueue<Action>();

    private static TcpListener listener;
    private static Thread listenerThread;
    private static bool running;

    static UnityDebugMcpVisualBridge()
    {
        EditorApplication.update += ProcessMainThreadActions;
        AssemblyReloadEvents.beforeAssemblyReload += StopListener;
        EditorApplication.quitting += StopListener;
    }

    [MenuItem(MenuPath)]
    public static void Start()
    {
        if (running)
        {
            return;
        }

        try
        {
            listener = new TcpListener(IPAddress.Loopback, Port);
            listener.Start();
            running = true;
            listenerThread = new Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "Unity Debug MCP Bridge"
            };
            listenerThread.Start();
            Debug.Log("Unity Debug MCP bridge listening on http://127.0.0.1:57579/");
        }
        catch (Exception exception)
        {
            StopListener();
            Debug.LogWarning("Unity Debug MCP bridge could not start: " + exception.Message);
        }
    }

    [MenuItem("Tools/Unity Debug MCP/Stop Visual Capture Bridge")]
    public static void Stop()
    {
        StopListener();
    }

    [MenuItem("Tools/Unity Debug MCP/Copy Weapon Rig Diagnostic")]
    public static void CopyWeaponRigDiagnostic()
    {
        GUIUtility.systemCopyBuffer = InspectWeaponRig();
        Debug.Log("Copied live weapon rig diagnostic to clipboard.");
    }

    private static void StopListener()
    {
        running = false;
        try
        {
            listener?.Stop();
        }
        catch (SocketException)
        {
            // The listener is already closing.
        }
        finally
        {
            listener = null;
            listenerThread = null;
        }
    }

    private static void ListenLoop()
    {
        while (running)
        {
            try
            {
                TcpClient client = listener.AcceptTcpClient();
                ThreadPool.QueueUserWorkItem(_ => HandleClient(client));
            }
            catch (SocketException)
            {
                // Expected while stopping the listener.
            }
            catch (ObjectDisposedException)
            {
                return;
            }
        }
    }

    private static void HandleClient(TcpClient client)
    {
        using (client)
        using (NetworkStream stream = client.GetStream())
        using (var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true))
        {
            string requestLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(requestLine))
            {
                return;
            }

            string header;
            do
            {
                header = reader.ReadLine();
            }
            while (!string.IsNullOrEmpty(header));

            string[] requestParts = requestLine.Split(' ');
            string target = requestParts.Length > 1 ? requestParts[1] : "/";
            string path = target.Split('?')[0];
            if (string.Equals(path, "/status", StringComparison.OrdinalIgnoreCase)
                || string.Equals(path, "/", StringComparison.OrdinalIgnoreCase))
            {
                string result = RunOnMainThread(() =>
                    "{\"ok\":true,\"available\":true,\"bridge\":\"UnityDebugMcpVisualBridge\",\"is_playing\":"
                    + (EditorApplication.isPlaying ? "true" : "false")
                    + "}");
                WriteJson(stream, 200, "OK", result);
                return;
            }

            if (string.Equals(path, "/commands", StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(
                    stream,
                    200,
                    "OK",
                    "{\"ok\":true,\"available\":true,\"commands\":[{\"name\":\"weapon.inspect\",\"description\":\"Reports live weapon rig axes and sockets.\"}]}");
                return;
            }

            if (string.Equals(path, "/trigger", StringComparison.OrdinalIgnoreCase)
                && target.IndexOf("command=weapon.inspect", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string result = RunOnMainThread(InspectWeaponRig);
                WriteJson(stream, 200, "OK", result);
                return;
            }

            WriteJson(stream, 404, "Not Found", "{\"ok\":false,\"error\":\"Unsupported endpoint.\"}");
        }
    }

    private static string RunOnMainThread(Func<string> action)
    {
        string result = null;
        Exception error = null;
        using (var completed = new ManualResetEventSlim(false))
        {
            MainThreadActions.Enqueue(() =>
            {
                try
                {
                    result = action();
                }
                catch (Exception exception)
                {
                    error = exception;
                }
                finally
                {
                    completed.Set();
                }
            });
            if (!completed.Wait(5000))
            {
                throw new TimeoutException("Unity main thread did not respond.");
            }
        }

        if (error != null)
        {
            throw error;
        }

        return result;
    }

    private static void ProcessMainThreadActions()
    {
        while (MainThreadActions.TryDequeue(out Action action))
        {
            action();
        }
    }

    private static string InspectWeaponRig()
    {
        WeaponRigSocketSet[] rigs = UnityEngine.Object.FindObjectsByType<WeaponRigSocketSet>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        var builder = new StringBuilder();
        builder.Append("{\"success\":true,\"message\":\"Live weapon rig inspection.\",\"values\":{");
        AppendValue(
            builder,
            "count",
            rigs.Length.ToString(CultureInfo.InvariantCulture));
        for (int index = 0; index < rigs.Length; index++)
        {
            WeaponRigSocketSet rig = rigs[index];
            string prefix = "rig_" + index.ToString(CultureInfo.InvariantCulture) + "_";
            AppendTransform(builder, prefix + "root", rig.transform);
            AppendTransform(builder, prefix + "visual", rig.VisualRoot);
            AppendTransform(builder, prefix + "muzzle", rig.Muzzle);
            AppendTransform(builder, prefix + "support", rig.SupportHand);
            AppendLeftIkDiagnostic(builder, prefix, rig);
        }

        builder.Append("}} ");
        return builder.ToString();
    }

    private static void AppendLeftIkDiagnostic(
        StringBuilder builder,
        string prefix,
        WeaponRigSocketSet rig)
    {
        Component driver = null;
        foreach (MonoBehaviour behaviour in
            rig.GetComponentsInParent<MonoBehaviour>(true))
        {
            if (behaviour != null
                && behaviour.GetType().Name == "WeaponRigIkDriver")
            {
                driver = behaviour;
                break;
            }
        }
        if (driver == null)
        {
            AppendValue(builder, prefix + "left_ik", "driver missing");
            return;
        }

        const BindingFlags Flags = BindingFlags.Instance
            | BindingFlags.NonPublic;
        Type type = driver.GetType();
        Transform hand = type.GetField("leftHand", Flags)?
            .GetValue(driver) as Transform;
        Transform upperArm = type.GetField("leftUpperArm", Flags)?
            .GetValue(driver) as Transform;
        Transform lowerArm = type.GetField("leftLowerArm", Flags)?
            .GetValue(driver) as Transform;
        Transform hint = type.GetField("leftElbowHint", Flags)?
            .GetValue(driver) as Transform;
        Transform target = type.GetField("leftTarget", Flags)?
            .GetValue(driver) as Transform;
        Component constraint = type.GetField("supportArmConstraint", Flags)?
            .GetValue(driver) as Component
            ?? type.GetField("leftHandConstraint", Flags)?
                .GetValue(driver) as Component;
        Component rigBuilder = type.GetField("rigBuilder", Flags)?
            .GetValue(driver) as Component;
        Component proceduralRig = type.GetField("proceduralRig", Flags)?
            .GetValue(driver) as Component;
        object blendWeight = type.GetField("blendWeight", Flags)?
            .GetValue(driver);
        AppendTransform(builder, prefix + "left_hand", hand);
        AppendTransform(builder, prefix + "left_upper_arm", upperArm);
        AppendTransform(builder, prefix + "left_lower_arm", lowerArm);
        AppendTransform(builder, prefix + "left_hint", hint);
        AppendTransform(builder, prefix + "left_target", target);
        AppendValue(builder, prefix + "left_blend", blendWeight?.ToString() ?? "null");
        AppendValue(
            builder,
            prefix + "driver_enabled",
            (driver as Behaviour)?.enabled.ToString() ?? "unknown");
        AppendValue(
            builder,
            prefix + "rig_weight",
            proceduralRig != null
                ? proceduralRig.GetType().GetProperty("weight")?
                    .GetValue(proceduralRig)?.ToString() ?? "unknown"
                : "missing");
        AppendValue(
            builder,
            prefix + "rig_builder_enabled",
            rigBuilder != null
                ? (rigBuilder as Behaviour)?.enabled.ToString() ?? "unknown"
                : "missing");
        AppendValue(
            builder,
            prefix + "left_constraint_weight",
            constraint != null
                ? constraint.GetType().GetProperty("weight")?
                    .GetValue(constraint)?.ToString() ?? "unknown"
                : "missing");
        AppendValue(
            builder,
            prefix + "left_hand_to_target",
            hand != null && target != null
                ? Vector3.Distance(hand.position, target.position)
                    .ToString("0.####", CultureInfo.InvariantCulture)
                : "null");
        AppendValue(
            builder,
            prefix + "left_upper_length",
            upperArm != null && lowerArm != null
                ? Vector3.Distance(upperArm.position, lowerArm.position)
                    .ToString("0.####", CultureInfo.InvariantCulture)
                : "null");
        AppendValue(
            builder,
            prefix + "left_lower_length",
            lowerArm != null && hand != null
                ? Vector3.Distance(lowerArm.position, hand.position)
                    .ToString("0.####", CultureInfo.InvariantCulture)
                : "null");
        AppendValue(
            builder,
            prefix + "left_root_to_support",
            upperArm != null && rig.SupportHand != null
                ? Vector3.Distance(upperArm.position, rig.SupportHand.position)
                    .ToString("0.####", CultureInfo.InvariantCulture)
                : "null");
    }

    private static void AppendTransform(StringBuilder builder, string name, Transform transform)
    {
        if (transform == null)
        {
            AppendValue(builder, name, "null");
            return;
        }

        AppendValue(builder, name + "_name", transform.name);
        AppendValue(builder, name + "_position", Format(transform.position));
        AppendValue(builder, name + "_rotation", Format(transform.rotation.eulerAngles));
        AppendValue(builder, name + "_right", Format(transform.right));
        AppendValue(builder, name + "_up", Format(transform.up));
        AppendValue(builder, name + "_forward", Format(transform.forward));
    }

    private static void AppendValue(StringBuilder builder, string name, string value)
    {
        if (builder[builder.Length - 1] != '{')
        {
            builder.Append(',');
        }

        builder.Append('"').Append(name).Append("\":\"")
            .Append(value.Replace("\\", "\\\\").Replace("\"", "\\\""))
            .Append('"');
    }

    private static string Format(Vector3 value)
    {
        return value.x.ToString("0.###", CultureInfo.InvariantCulture)
            + "," + value.y.ToString("0.###", CultureInfo.InvariantCulture)
            + "," + value.z.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static void WriteJson(NetworkStream stream, int statusCode, string status, string json)
    {
        byte[] body = Encoding.UTF8.GetBytes(json);
        string headers = "HTTP/1.1 " + statusCode.ToString(CultureInfo.InvariantCulture)
            + " " + status + "\r\nContent-Type: application/json\r\nContent-Length: "
            + body.Length.ToString(CultureInfo.InvariantCulture)
            + "\r\nConnection: close\r\n\r\n";
        byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
        stream.Write(headerBytes, 0, headerBytes.Length);
        stream.Write(body, 0, body.Length);
    }
}
#endif
