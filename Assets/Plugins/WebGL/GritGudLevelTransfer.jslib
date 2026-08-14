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

  GritGud_RequestTextFile: function (gameObjectNamePointer) {
    var gameObjectName = UTF8ToString(gameObjectNamePointer);
    var input = document.createElement("input");
    input.type = "file";
    input.accept = ".json,application/json,text/plain";
    input.style.display = "none";
    input.addEventListener("change", function () {
      if (!input.files || input.files.length === 0) {
        document.body.removeChild(input);
        return;
      }

      var reader = new FileReader();
      reader.onload = function () {
        SendMessage(gameObjectName, "ReceiveImportedLevelText", reader.result);
        document.body.removeChild(input);
      };
      reader.onerror = function () {
        SendMessage(gameObjectName, "ReceiveLevelImportError", "The browser could not read that file.");
        document.body.removeChild(input);
      };
      reader.readAsText(input.files[0]);
    });
    document.body.appendChild(input);
    input.click();
  }
});
