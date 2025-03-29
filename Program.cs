
using NAudio.Wave;
using NAudio.CoreAudioApi;
using Vosk;
using System.Linq;
using System.Text;
using System.Text.Json;

public class Prog {
  public static void Main() {
    var modelPath = "vosk-model-small-ja-0.22";
    var wavFileName = "audio.wav";
    var mp3FileName = wavFileName.Replace(".wav", ".mp3");
    var txtFileName = "audio.txt";
    if (Directory.Exists(modelPath)) {
      Recording(wavFileName);
      ConvertMP3(wavFileName, mp3FileName);
      Sampling(wavFileName, txtFileName, modelPath);
    }
    else {
      Console.WriteLine("「{0}」が存在しません。https://alphacephei.com/vosk/models からダウンロードしてください。", modelPath);
    }
  }

  private static void Recording(string wavFileName) {
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
          Console.Write("「{0}」の録音を開始します（Enterキーで停止）。", device.FriendlyName);
          wasapiCapture.StartRecording();
          Console.ReadLine();
          wasapiCapture.StopRecording();
        }
      }
    }
  }

  private static void ConvertMP3(string wavFileName, string mp3FileName) {
    Console.WriteLine("wavファイルをmp3ファイルに変換します。");
    using (var reader = new WaveFileReader(wavFileName)) {
      MediaFoundationEncoder.EncodeToMp3(reader, mp3FileName);
    }
  }

  private static void Sampling(string wavFileName, string txtFileName, string modelPath) {
    Console.WriteLine("==============================================");
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
              Console.WriteLine(result);
              lines.Add(result);
            }
          }
        }
        var finalResult = GetTextOf(recognizer.FinalResult());
        if (!String.IsNullOrEmpty(finalResult)) {
          Console.WriteLine(finalResult);
          lines.Add(finalResult);
        }
      }
    }
    File.WriteAllLines(txtFileName, lines.ToArray(), Encoding.UTF8);
    Console.WriteLine("==============================================");
  }

  private static string GetTextOf(String json) {
    var result = JsonDocument.Parse(json);
    return (result.RootElement.GetProperty("text").GetString() ?? "").Replace(" ", "");
  }
}
