
using NAudio.Wave;
using NAudio.CoreAudioApi;

public class Prog {
  public static void Main() {
    var wavFileName = "audio.wav";
    var mp3FileName = wavFileName.Replace(".wav", ".mp3");
    var i = 0;
    var activeMMDevices = new List<Tuple<MMDevice, string>>();
    using (var enumerator = new MMDeviceEnumerator()) {
      Console.WriteLine("録音するデバイスを選択してください。");
      foreach (var wasapi in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)) {
        try {
          var s = String.Format("{0}. {1}", i + 1, wasapi.FriendlyName);
          activeMMDevices.Add(new Tuple<MMDevice, string>(wasapi, s));
          Console.WriteLine("  {0}", s);
          i++;
        }
        catch (Exception) {}
      }
      Console.Write(">");
      var selected = Console.ReadLine();
      if (!String.IsNullOrEmpty(selected)) {
        foreach (var tpl in activeMMDevices) {
          if (tpl.Item2.StartsWith(String.Format("{0}. ", selected))) {
            using (var wasapiCapture = new WasapiLoopbackCapture(tpl.Item1)) {
              using (var writer = new WaveFileWriter(wavFileName, wasapiCapture.WaveFormat)) {
                wasapiCapture.DataAvailable += (s, e) => {
                  writer.Write(e.Buffer, 0, e.BytesRecorded);
                };
                wasapiCapture.RecordingStopped += (s, e) => {
                  writer.Dispose();
                  wasapiCapture.Dispose();
                };
                Console.Write("録音を開始します（Enterキーで停止）。");
                wasapiCapture.StartRecording();
                Console.ReadLine();
                wasapiCapture.StopRecording();
              }
            }
          }
        }
        Console.WriteLine("録音を終了しました。");
        Console.WriteLine("wavファイルをmp3ファイルに変換します。");
        using (var reader = new WaveFileReader(wavFileName)) {
          MediaFoundationEncoder.EncodeToMp3(reader, mp3FileName);
        }
        Console.WriteLine("mp3ファイルに変換しました。");
        using (var reader = new Mp3FileReader(mp3FileName))
        {
            var len = (new FileInfo(mp3FileName)).Length;
            var fmb = len / 1024.0 / 1024.0;
            var fkb = len / 1024.0;
            var totalTime = reader.TotalTime;
            var waveFormat = reader.Mp3WaveFormat;
            Console.WriteLine();
            Console.WriteLine("=====================================");
            Console.WriteLine("ファイル名        : {0}", mp3FileName);
            if (fmb < 1) {
              Console.WriteLine("ファイルサイズ    : {0} KB",  Math.Floor(fkb * 10) / 10);
            }
            else {
              Console.WriteLine("ファイルサイズ    : {0} MB",  Math.Floor(fmb * 10) / 10);
            }
            Console.WriteLine("長さ              : {0:D2}:{1:D2}:{2:D2}", totalTime.Hours, totalTime.Minutes, totalTime.Seconds);
            Console.WriteLine("サンプリングレート: {0} Hz", waveFormat.SampleRate);
            Console.WriteLine("チャンネル数      : {0} ch", waveFormat.Channels);
            Console.WriteLine("ビットレート      : {0} kbps", waveFormat.AverageBytesPerSecond * 8 / 1000);
            Console.WriteLine("=====================================");
        }
        File.Delete(wavFileName);
      }
    }
  }
}
