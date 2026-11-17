// Automatisch erzeugt aus overlay/nova.html mit tools/embed-overlay.py. Nicht von Hand ändern.
namespace DebateAI
{
    internal static class OverlayPage
    {
        public const string Html = @"<!DOCTYPE html>
<html lang=""de"">
<head>
<meta charset=""utf-8"">
<title>DEBATE AI – NOVA LIVE</title>
<style>
html, body { margin: 0; padding: 0; width: 100%; height: 100%; overflow: hidden; background: transparent; }
body { font-family: ""Segoe UI"", Arial, sans-serif; color: #eaf6ff; }
#stage { position: absolute; left: 0; top: 0; width: 1080px; height: 1920px; transform-origin: 0 0; -webkit-transform-origin: 0 0; }
.scene #stage {
  background-color: #05060f;
  background-image: radial-gradient(circle at 20% 18%, rgba(0, 229, 255, 0.22), rgba(0, 0, 0, 0) 42%),
                    radial-gradient(circle at 82% 40%, rgba(170, 70, 255, 0.24), rgba(0, 0, 0, 0) 45%),
                    radial-gradient(circle at 50% 95%, rgba(0, 229, 255, 0.12), rgba(0, 0, 0, 0) 50%);
}
#title { position: absolute; left: 0; width: 1080px; top: 60px; text-align: center; font-size: 112px; font-weight: 900; letter-spacing: 4px;
  color: #6ff3ff; text-shadow: 0 0 18px rgba(0, 229, 255, 0.85), 0 0 46px rgba(170, 70, 255, 0.8); }
#subtitle { position: absolute; left: 0; width: 1080px; top: 200px; text-align: center; font-size: 40px; color: #c9b8ff; letter-spacing: 2px; }
#orb { position: absolute; left: 230px; top: 260px; width: 620px; height: 620px; }
#name { position: absolute; left: 0; width: 1080px; top: 888px; text-align: center; font-size: 76px; font-weight: 800; letter-spacing: 12px; color: #ffffff;
  text-shadow: 0 0 22px rgba(170, 70, 255, 0.9); }
#status { position: absolute; left: 0; width: 1080px; top: 985px; text-align: center; font-size: 40px; color: #6ff3ff; }
#label { position: absolute; left: 90px; width: 900px; top: 1052px; text-align: center; font-size: 33px; padding: 12px 0; border-radius: 40px;
  background: rgba(10, 12, 30, 0.82); border: 2px solid rgba(111, 243, 255, 0.7); color: #eaf6ff; }
#topicBox { position: absolute; left: 70px; width: 940px; top: 1138px; text-align: center; }
#topicHead { font-size: 26px; letter-spacing: 6px; color: #c9b8ff; }
#topic { font-size: 46px; font-weight: 700; color: #ffffff; max-height: 120px; overflow: hidden; }
#guest { position: absolute; left: 60px; width: 952px; top: 1290px; height: 572px; border: 4px solid rgba(111, 243, 255, 0.65); border-radius: 36px;
  box-shadow: 0 0 34px rgba(0, 229, 255, 0.35), inset 0 0 34px rgba(170, 70, 255, 0.25); }
#guestTag { position: absolute; left: 40px; top: -30px; padding: 6px 26px; font-size: 32px; font-weight: 800; letter-spacing: 6px; border-radius: 20px;
  background: #aa46ff; color: #ffffff; }
.badge #title { font-size: 66px; top: 24px; left: 40px; width: 1000px; padding: 10px 0; border-radius: 30px; background: rgba(5, 6, 15, 0.72); }
.badge #subtitle, .badge #topicBox, .badge #guest, .badge #name { display: none; }
.badge #orb { left: 40px; top: 150px; width: 260px; height: 260px; }
.badge #orb { border-radius: 50%; background: rgba(5, 6, 15, 0.55); }
.badge #status { left: 320px; width: 660px; top: 205px; text-align: left; padding: 8px 20px; border-radius: 24px; background: rgba(5, 6, 15, 0.72); }
.badge #label { left: 320px; width: 700px; top: 290px; font-size: 27px; }
.hidden { display: none; }
</style>
</head>
<body class=""scene"">
<div id=""stage"">
  <div id=""title"">DU GEGEN NOVA</div>
  <div id=""subtitle"">Debattiere LIVE mit einer KI</div>
  <canvas id=""orb"" width=""620"" height=""620""></canvas>
  <div id=""name"">NOVA</div>
  <div id=""status"">bereit</div>
  <div id=""label"">NOVA · KI-Debattenpartnerin · synthetische Stimme</div>
  <div id=""topicBox"" class=""hidden""><div id=""topicHead"">THEMA</div><div id=""topic""></div></div>
  <div id=""guest""><div id=""guestTag"">GAST</div></div>
</div>
<script>
(function () {
  var query = window.location.search || """";
  if (query.indexOf(""view=badge"") >= 0) document.body.className = ""badge"";
  var stage = document.getElementById(""stage"");
  var canvas = document.getElementById(""orb"");
  var ctx = canvas.getContext(""2d"");
  var state = { name: ""Nova"", topic: """", phase: ""idle"", level: 0, running: false };
  var online = false, level = 0, spin = 0, glow = 0;
  var texts = { speaking: ""spricht"", thinking: ""denkt nach …"", guest: ""hört dir zu"", listening: ""hört zu"", connecting: ""verbindet …"", paused: ""Pause"", idle: ""bereit"" };

  function fit() {
    var scale = Math.min(window.innerWidth / 1080, window.innerHeight / 1920);
    if (!(scale > 0)) scale = 1;
    var value = ""scale("" + scale + "")"";
    stage.style.transform = value; stage.style.webkitTransform = value;
  }
  window.onresize = fit; fit();

  function setText(id, text) { var node = document.getElementById(id); if (node.textContent !== text) node.textContent = text; }

  function apply() {
    var name = (state.name || ""Nova"").toUpperCase();
    setText(""name"", name);
    setText(""status"", online ? name.charAt(0) + name.substring(1).toLowerCase() + "" "" + (texts[state.phase] || ""bereit"") : ""DEBATE AI startet …"");
    var topic = state.topic || """";
    document.getElementById(""topicBox"").className = topic.length ? """" : ""hidden"";
    setText(""topic"", topic);
  }

  function poll() {
    var request = new XMLHttpRequest();
    request.onreadystatechange = function () {
      if (request.readyState !== 4) return;
      if (request.status === 200) {
        try { state = JSON.parse(request.responseText); online = true; } catch (e) { online = false; }
      } else online = false;
      apply();
    };
    request.open(""GET"", ""/state?t="" + new Date().getTime(), true);
    request.timeout = 1500;
    try { request.send(); } catch (e) { online = false; }
  }
  setInterval(poll, 100);
  poll();

  function ring(radius, width, start, length, color) {
    ctx.beginPath();
    ctx.arc(310, 310, radius, start, start + length, false);
    ctx.lineWidth = width; ctx.strokeStyle = color; ctx.stroke();
  }

  function oval(x, y, rx, ry) {
    ctx.save(); ctx.translate(x, y); ctx.scale(1, Math.max(0.05, ry / rx));
    ctx.beginPath(); ctx.arc(0, 0, rx, 0, Math.PI * 2, false); ctx.restore(); ctx.fill();
  }

  function draw() {
    var speaking = online && state.phase === ""speaking"";
    var thinking = online && (state.phase === ""thinking"" || state.phase === ""connecting"");
    var listening = online && (state.phase === ""guest"" || state.phase === ""listening"");
    var target = speaking ? Math.max(0.08, state.level || 0) : 0;
    level += (target - level) * (target > level ? 0.55 : 0.18);
    glow += ((speaking ? 1 : thinking ? 0.7 : listening ? 0.45 : 0.25) - glow) * 0.08;
    spin += speaking ? 0.035 + level * 0.05 : thinking ? 0.06 : 0.012;
    ctx.clearRect(0, 0, 620, 620);
    var core = 150 + level * 40;
    var halo = ctx.createRadialGradient(310, 310, core * 0.3, 310, 310, 300);
    halo.addColorStop(0, ""rgba(111,243,255,"" + (0.55 * glow + 0.15) + "")"");
    halo.addColorStop(0.45, ""rgba(170,70,255,"" + (0.35 * glow + 0.08) + "")"");
    halo.addColorStop(1, ""rgba(5,6,15,0)"");
    ctx.fillStyle = halo; ctx.fillRect(0, 0, 620, 620);
    ring(245, 6, spin, 2.2, ""rgba(111,243,255,0.85)"");
    ring(245, 6, spin + 3.3, 1.4, ""rgba(170,70,255,0.85)"");
    ring(275, 3, -spin * 0.7, 3.6, ""rgba(170,70,255,0.55)"");
    ring(212, 3, -spin * 1.3 + 1, 2.6, ""rgba(111,243,255,0.5)"");
    var body = ctx.createRadialGradient(280, 270, 20, 310, 310, core);
    body.addColorStop(0, ""#e9fdff"");
    body.addColorStop(0.35, ""#5fe9ff"");
    body.addColorStop(0.8, ""#6a38d8"");
    body.addColorStop(1, ""#1a1040"");
    ctx.beginPath(); ctx.arc(310, 310, core, 0, Math.PI * 2, false); ctx.fillStyle = body; ctx.fill();
    // Abstract face: two round light eyes and a mouth that opens with NOVA's voice level.
    ctx.fillStyle = ""rgba(5,6,15,0.78)"";
    var open = (Math.floor(spin * 10) % 47 === 0) ? 0.2 : 1;
    oval(272, 285, 18, 18 * open);
    oval(348, 285, 18, 18 * open);
    oval(310, 350, 46, 5 + level * 26);
    if (listening) { ring(core + 18, 4, 0, Math.PI * 2, ""rgba(255,255,255,"" + (0.25 + 0.2 * Math.sin(spin * 8)) + "")""); }
  }
  setInterval(draw, 33);
})();
</script>
</body>
</html>
";
    }
}
