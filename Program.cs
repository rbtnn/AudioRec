
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Lame;
using Vosk;
using System.Linq;
using System.Text;
using System.Text.Json;

public class Prog {
  public static void Main(string[] args) {
    var dt = DateTime.Now;
    var modelPath = "vosk-model-small-ja-0.22";
    var outdir = "output";
    var wavFileName =  Path.Combine(outdir, dt.ToString("yyyyMMdd-HHmmss") + ".wav");
    var mp3FileName = wavFileName.Replace(".wav", ".mp3");
    var txtFileName = wavFileName.Replace(".wav", ".txt");
    if (!Directory.Exists(outdir)) {
      Directory.CreateDirectory(outdir);
    }
    Recording(wavFileName);
    SpeechToText(wavFileName, txtFileName, modelPath);
    ConvertMP3(wavFileName, mp3FileName);
  }

  private static void Recording(string wavFileName) {
    Console.WriteLine("[Recording]");
    Console.WriteLine("  File Name: {0}", wavFileName);
    using (var enumerator = new MMDeviceEnumerator()) {
      var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
      using (var wasapiCapture = new WasapiLoopbackCapture(device)) {
        using (var writer = new WaveFileWriter(wavFileName, wasapiCapture.WaveFormat)) {
          wasapiCapture.DataAvailable += (s, e) => {
            writer.Write(e.Buffer, 0, e.BytesRecorded);
          };
          wasapiCapture.RecordingStopped += (s, e) => {
            writer.Dispose();
            wasapiCapture.Dispose();
          };
          Console.WriteLine("  Device Name: {0}", device.FriendlyName);
          Console.Write("  録音開始(Enterキーで停止):");
          wasapiCapture.StartRecording();
          Console.ReadLine();
          wasapiCapture.StopRecording();
          Console.WriteLine("  録音終了");
        }
      }
    }
    Console.WriteLine();
  }

  private static void ConvertMP3(string wavFileName, string mp3FileName) {
    Console.WriteLine("[ConvertMP3]");
    using (var reader = new WaveFileReader(wavFileName)) {
      using (var writer = new LameMP3FileWriter(mp3FileName, reader.WaveFormat, LAMEPreset.VBR_90)) {
        reader.CopyTo(writer);
      }
    }
    using (var reader = new Mp3FileReader(mp3FileName)) {
      var len = (new FileInfo(mp3FileName)).Length;
      var fmb = len / 1024.0 / 1024.0;
      var fkb = len / 1024.0;
      var totalTime = reader.TotalTime;
      var waveFormat = reader.Mp3WaveFormat;
      Console.WriteLine("  File Name: {0}", mp3FileName);
      Console.WriteLine("  File Size: {0} {1}",  Math.Floor((fmb < 1 ? fkb : fmb) * 10) / 10, (fmb < 1 ? "KB" : "MB"));
      Console.WriteLine("  Duration: {0:D2}:{1:D2}:{2:D2}", totalTime.Hours, totalTime.Minutes, totalTime.Seconds);
      Console.WriteLine("  Sample Rate: {0} Hz", waveFormat.SampleRate);
      Console.WriteLine("  Channels: {0} ch", waveFormat.Channels);
      Console.WriteLine("  Bit Rate: {0} kbps", waveFormat.AverageBytesPerSecond * 8 / 1000);
    }
    Console.WriteLine();
  }

  private static void SpeechToText(string wavFileName, string txtFileName, string modelPath) {
    if (Directory.Exists(modelPath)) {
      Console.WriteLine("[SpeechToText]");
      Console.WriteLine("  File Name: {0}", txtFileName);
      Console.WriteLine("  Model: {0} (https://alphacephei.com/vosk/models)", modelPath);
      Vosk.Vosk.SetLogLevel(-1);
      var model = new Model(modelPath);
      var lines = new List<string>();
      using (var waveReader = new WaveFileReader(wavFileName)) {
        using (var resampler = new MediaFoundationResampler(waveReader, new WaveFormat(16000, 16, 1))) {
          resampler.ResamplerQuality = 60;
          var recognizer = new VoskRecognizer(model, 16000.0f);
          var buffer = new byte[4096];
          while (true) {
            var bytesRead = resampler.Read(buffer, 0, buffer.Length);
            if (bytesRead == 0) {
              break;
            }
            if (recognizer.AcceptWaveform(buffer, bytesRead)) {
              var result = GetTextOf(recognizer.Result());
              if (!String.IsNullOrEmpty(result)) {
                lines.Add(result);
              }
            }
          }
          var finalResult = GetTextOf(recognizer.FinalResult());
          if (!String.IsNullOrEmpty(finalResult)) {
            lines.Add(finalResult);
          }
        }
      }
      File.WriteAllLines(txtFileName, lines.ToArray(), Encoding.UTF8);
      Console.WriteLine();
    }
  }

  private static string GetTextOf(String json) {
    var result = JsonDocument.Parse(json);
    return (result.RootElement.GetProperty("text").GetString() ?? "").Replace(" ", "");
  }
}

