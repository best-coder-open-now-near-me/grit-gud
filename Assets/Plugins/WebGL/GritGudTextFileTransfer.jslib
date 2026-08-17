mergeInto(LibraryManager.library, {
  GritGud_DownloadTextFile: function (fileNamePointer, contentPointer, mediaTypePointer) {
    var fileName = UTF8ToString(fileNamePointer);
    var content = UTF8ToString(contentPointer);
    var mediaType = UTF8ToString(mediaTypePointer);
    var blob = new Blob([content], { type: mediaType });
    var url = URL.createObjectURL(blob);
    var anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = fileName;
    anchor.style.display = "none";
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
    setTimeout(function () { URL.revokeObjectURL(url); }, 0);
  },

  GritGud_RequestTextFile: function (gameObjectNamePointer, requestIdPointer) {
    var gameObjectName = UTF8ToString(gameObjectNamePointer);
    var requestId = UTF8ToString(requestIdPointer);
    var input = document.createElement("input");
    var removed = false;
    var focusHandler = null;
    var complete = function (method, value) {
      if (removed) {
        return;
      }
      removed = true;
      if (focusHandler) {
        window.removeEventListener("focus", focusHandler);
      }
      SendMessage(gameObjectName, method, requestId + "\n" + value);
      if (input.parentNode) {
        input.parentNode.removeChild(input);
      }
    };
    input.type = "file";
    input.accept = ".json,application/json,text/plain";
    input.style.display = "none";
    input.addEventListener("change", function () {
      if (!input.files || input.files.length === 0) {
        complete("ReceiveTextImportError", "Text-file import was cancelled.");
        return;
      }

      var reader = new FileReader();
      reader.onload = function () {
        complete("ReceiveImportedText", reader.result);
      };
      reader.onerror = function () {
        complete("ReceiveTextImportError", "The browser could not read that file.");
      };
      reader.readAsText(input.files[0]);
    });
    input.addEventListener("cancel", function () {
      complete("ReceiveTextImportError", "Text-file import was cancelled.");
    });
    focusHandler = function () {
      setTimeout(function () {
        if (!removed && (!input.files || input.files.length === 0)) {
          complete("ReceiveTextImportError", "Text-file import was cancelled.");
        }
      }, 250);
    };
    window.addEventListener("focus", focusHandler);
    document.body.appendChild(input);
    input.click();
  }
});
