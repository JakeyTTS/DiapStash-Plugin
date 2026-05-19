using System;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DiapStash_Plugin
{
    public class OverlayServer
    {
        private static OverlayServer? _instance;
        public static OverlayServer Instance => _instance ??= new OverlayServer();

        private HttpListener? _listener;
        private bool _isRunning = false;

        public double CardW { get; set; } = 800; public double CardH { get; set; } = 450;
        public int TransitionType { get; set; } = 0; public double TransitionDurationMs { get; set; } = 400;
        public bool ForcePreviewTrigger { get; set; } = false;
        public string Title { get; set; } = "TELEMETRY";
        public string ProductName { get; set; } = "Product Name (Size M)";
        public string ImageUrl { get; set; } = "";
        public int RealWetPercentage { get; set; } = 50;
        public int RealMessPercentage { get; set; } = 20;
        public string WetColor { get; set; } = "#00BFFF";
        public string MessColor { get; set; } = "#CD853F";
        public bool ShowWetBar { get; set; } = true;
        public bool ShowMessBar { get; set; } = true;
        public double BarW { get; set; } = 200;
        public double ImgX { get; set; } = 50; public double ImgY { get; set; } = 50;
        public double ImgW { get; set; } = 80; public double ImgH { get; set; } = 80;
        public double TxtX { get; set; } = 150; public double TxtY { get; set; } = 50;
        public double ProdX { get; set; } = 150; public double ProdY { get; set; } = 80;
        public double BarsX { get; set; } = 150; public double BarsY { get; set; } = 110;

        public void Start()
        {
            if (_isRunning) return;
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://localhost:8890/overlay/");
            try { _listener.Start(); _isRunning = true; Task.Run(ListenLoop); } catch { }
        }

        private async Task ListenLoop()
        {
            while (_isRunning)
            {
                try
                {
                    var ctx = await _listener!.GetContextAsync();
                    var resp = ctx.Response;
                    resp.Headers.Add("Access-Control-Allow-Origin", "*");

                    if (ctx.Request.Url!.AbsolutePath == "/overlay/trigger")
                    {
                        ForcePreviewTrigger = true;
                        resp.StatusCode = 200;
                        resp.ContentLength64 = 0;
                    }
                    else if (ctx.Request.Url!.AbsolutePath == "/overlay/config")
                    {
                        var data = new { cardW = CardW, cardH = CardH, transType = TransitionType, transDur = TransitionDurationMs, title = Title, product = ProductName, imgUrl = ImageUrl, wetP = RealWetPercentage, messP = RealMessPercentage, wetCol = WetColor, messCol = MessColor, showWet = ShowWetBar, showMess = ShowMessBar, imgX = ImgX, imgY = ImgY, imgW = ImgW, imgH = ImgH, txtX = TxtX, txtY = TxtY, prodX = ProdX, prodY = ProdY, barsX = BarsX, barsY = BarsY, barW = BarW, trigger = ForcePreviewTrigger };
                        if (ForcePreviewTrigger) ForcePreviewTrigger = false;
                        byte[] b = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data));
                        await resp.OutputStream.WriteAsync(b, 0, b.Length);
                    }
                    else
                    {
                        byte[] h = Encoding.UTF8.GetBytes(GetHtml());
                        await resp.OutputStream.WriteAsync(h, 0, h.Length);
                    }
                    resp.Close();
                }
                catch { }
            }
        }

        private string GetHtml() => @"<!DOCTYPE html><html><head><style>
            body { background: transparent; overflow: hidden; margin: 0; font-family: sans-serif; display: flex; align-items: center; justify-content: center; height: 100vh; }
            :root { --dur: 0.4s; }
            .canvas { position: relative; background: white; border-radius: 12px; opacity: 0; box-shadow: 0 4px 20px rgba(0,0,0,0.15); transition: all var(--dur) ease-out; }
            .canvas[data-anim='1'] { transform: scale(0.8); }
            .canvas[data-anim='2'] { transform: translateX(-50px); }
            .canvas[data-anim='3'] { transform: translateX(50px); }
            .canvas[data-anim='4'] { transform: translateY(-50px); }
            .canvas[data-anim='5'] { transform: translateY(50px); }
            .canvas[data-anim='6'] { transform: scale(0.5); }
            .canvas.active { opacity: 1; transform: scale(1) translate(0, 0); }
            .canvas.active[data-anim='6'] { transition-timing-function: cubic-bezier(0.175, 0.885, 0.32, 1.275); }
            .abs { position: absolute; }
            .img-box { background: #ddd; border-radius: 8px; display: flex; align-items: center; justify-content: center; overflow: hidden; }
            .bar { height: 10px; background: #eee; border-radius: 5px; margin-top: 5px; }
            .fill { height: 100%; border-radius: 5px; transition: width 1s ease-out; }
        </style></head><body>
            <div id='c' class='canvas'>
                <div id='i' class='abs img-box'><span id='icon'>🛒</span><img id='real-img' src='' style='width:100%; height:100%; object-fit:cover; display:none;'/></div>
                <div id='txt' class='abs'><h2 id='t' style='margin:0;'></h2></div>
                <div id='prod' class='abs'><p id='p_name' style='margin:0; color:#666;'></p></div>
                <div id='m' class='abs'></div>
            </div>
            <script>
                const c = document.getElementById('c'), m = document.getElementById('m');
                async function sync() {
                    const d = await (await fetch('http://localhost:8890/overlay/config?t='+Date.now())).json();
                    c.style.width = d.cardW + 'px'; c.style.height = d.cardH + 'px';
                    document.documentElement.style.setProperty('--dur', (d.transDur / 1000) + 's');
                    c.setAttribute('data-anim', d.transType);
                    document.getElementById('t').innerText = d.title;
                    document.getElementById('p_name').innerText = d.product;
                    const img = document.getElementById('i');
                    img.style.left = d.imgX + 'px'; img.style.top = d.imgY + 'px';
                    img.style.width = d.imgW + 'px'; img.style.height = d.imgH + 'px';
                    if(d.imgUrl) { document.getElementById('real-img').src=d.imgUrl; document.getElementById('real-img').style.display='block'; document.getElementById('icon').style.display='none'; }
                    document.getElementById('txt').style.left = d.txtX + 'px'; document.getElementById('txt').style.top = d.txtY + 'px';
                    document.getElementById('prod').style.left = d.prodX + 'px'; document.getElementById('prod').style.top = d.prodY + 'px';
                    m.style.left = d.barsX + 'px'; m.style.top = d.barsY + 'px';
                    m.innerHTML = `<div style='color:${d.wetCol}'>Wet <span style='float:right'>${d.wetP}%</span><div class='bar' style='width:${d.barW}px'><div class='fill' style='width:${d.wetP}%; background:${d.wetCol}'></div></div></div>
                                   <div style='color:${d.messCol}'>Mess <span style='float:right'>${d.messP}%</span><div class='bar' style='width:${d.barW}px'><div class='fill' style='width:${d.messP}%; background:${d.messCol}'></div></div></div>`;
                    if(d.trigger) { c.classList.add('active'); setTimeout(()=>c.classList.remove('active'), 6000); }
                }
                setInterval(sync, 800);
            </script>
        </body></html>";
    }
}