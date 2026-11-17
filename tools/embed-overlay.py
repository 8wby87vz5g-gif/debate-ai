# Erzeugt windows/src/OverlayPage.cs aus windows/overlay/nova.html (eine Quelle, eingebettet in die EXE).
import pathlib
root = pathlib.Path(__file__).resolve().parent.parent / "windows"
html = (root / "overlay" / "nova.html").read_text(encoding="utf-8").replace("\r\n", "\n")
code = ("// Automatisch erzeugt aus overlay/nova.html mit tools/embed-overlay.py. Nicht von Hand ändern.\n"
        "namespace DebateAI\n{\n    internal static class OverlayPage\n    {\n        public const string Html = @\""
        + html.replace('"', '""') + "\";\n    }\n}\n")
(root / "src" / "OverlayPage.cs").write_text(code, encoding="utf-8", newline="\n")
print("OverlayPage.cs:", len(html), "Zeichen")
