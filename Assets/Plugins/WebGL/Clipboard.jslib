mergeInto(LibraryManager.library, {
  // Called from Clipboard.Copy. The async Clipboard API needs a secure context (https or localhost);
  // anywhere else, or if it is refused, fall back to a hidden textarea and execCommand.
  ClipboardCopy: function (textPtr) {
    var text = UTF8ToString(textPtr);

    function fallback() {
      var area = document.createElement("textarea");
      area.value = text;
      area.setAttribute("readonly", "");
      area.style.position = "fixed";
      area.style.opacity = "0";
      document.body.appendChild(area);
      area.select();
      try { document.execCommand("copy"); } catch (e) {}
      document.body.removeChild(area);
    }

    if (navigator.clipboard && window.isSecureContext) {
      navigator.clipboard.writeText(text).catch(fallback);
    } else {
      fallback();
    }
  }
});
