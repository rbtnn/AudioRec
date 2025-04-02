using NAudio.CoreAudioApi;
using NAudio.Lame;
using NAudio.Wave.SampleProviders;
using NAudio.Wave;
using System.Linq;

public class Prog
{
    public static void Main(string[] args)
    {
        var dt = DateTime.Now;
        var outdir = "output";
        var tmpdir = "tmp";
        var speakerFileName = Path.Combine(tmpdir, "speaker.wav");
        var micFileName = Path.Combine(tmpdir, "mic.wav");
        var wavFileName = Path.Combine(outdir, dt.ToString("yyyyMMdd-HHmmss") + ".wav");
        var mp3FileName = wavFileName.Replace(".wav", ".mp3");
        var txtFileName = wavFileName.Replace(".wav", ".txt");
        if (!Directory.Exists(outdir))
        {
            Directory.CreateDirectory(outdir);
        }
        if (!Directory.Exists(tmpdir))
        {
            Directory.CreateDirectory(tmpdir);
        }
        Recording(wavFileName, speakerFileName, micFileName);
        ConvertMP3(wavFileName, mp3FileName);
        try
        {
            File.Delete(speakerFileName);
            File.Delete(micFileName);
            Directory.Delete(tmpdir);
        }
        catch (Exception) { }
    }

    private static void RecordingMix(string mixFileName, string speakerFileName, string micFileName)
    {
        using (var micReader = new AudioFileReader(micFileName))
        {
            using (var speakerReader = new AudioFileReader(speakerFileName))
            {
                var targetFormat = new WaveFormat(44100, 2);
                var micResampler = new MediaFoundationResampler(micReader, targetFormat);
                var speakerResampler = new MediaFoundationResampler(speakerReader, targetFormat);
                var mixer = new MixingSampleProvider(new[] {
            micResampler.ToSampleProvider(),
            speakerResampler.ToSampleProvider()
            });
                WaveFileWriter.CreateWaveFile16(mixFileName, mixer);
            }
        }
    }

    private static void Recording(string mixFileName, string speakerFileName, string micFileName)
    {
        Console.WriteLine("[Recording]");
        Console.WriteLine("  File Name: {0}", mixFileName);
        using (var enumerator = new MMDeviceEnumerator())
        {
            var speakerDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
            var speakerCapture = new WasapiLoopbackCapture(speakerDevice);
            var micDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
            var micCapture = new WasapiCapture(micDevice);
            using (var speakerWriter = new WaveFileWriter(speakerFileName, speakerCapture.WaveFormat))
            {
                using (var micWriter = new WaveFileWriter(micFileName, micCapture.WaveFormat))
                {
                    speakerCapture.DataAvailable += (s, e) =>
                    {
                        speakerWriter.Write(e.Buffer, 0, e.BytesRecorded);
                    };
                    speakerCapture.RecordingStopped += (s, e) =>
                    {
                        speakerCapture.Dispose();
                        speakerWriter.Dispose();
                    };

                    micCapture.DataAvailable += (s, e) =>
                    {
                        micWriter.Write(e.Buffer, 0, e.BytesRecorded);
                    };
                    micCapture.RecordingStopped += (s, e) =>
                    {
                        micCapture.Dispose();
                        micWriter.Dispose();
                    };

                    Console.WriteLine("  Speaker Device Name: {0}", speakerDevice.FriendlyName);
                    Console.WriteLine("  Mic Device Name: {0}", micDevice.FriendlyName);
                    Console.Write("  録音開始(Enterキーで停止):");
                    speakerCapture.StartRecording();
                    micCapture.StartRecording();
                    Console.ReadLine();
                    speakerCapture.StopRecording();
                    micCapture.StopRecording();
                    Console.WriteLine("  録音終了");
                }
            }
        }
        RecordingMix(mixFileName, speakerFileName, micFileName);
        Console.WriteLine();
    }

    private static void ConvertMP3(string wavFileName, string mp3FileName)
    {
        using (var reader = new WaveFileReader(wavFileName))
        {
            using (var writer = new LameMP3FileWriter(mp3FileName, reader.WaveFormat, LAMEPreset.VBR_90))
            {
                reader.CopyTo(writer);
            }
        }
        using (var reader = new Mp3FileReader(mp3FileName))
        {
            var len = (new FileInfo(mp3FileName)).Length;
            var fmb = len / 1024.0 / 1024.0;
            var fkb = len / 1024.0;
            var totalTime = reader.TotalTime;
            var waveFormat = reader.Mp3WaveFormat;
            Console.WriteLine("[ConvertMP3]");
            Console.WriteLine("  File Name: {0}", mp3FileName);
            Console.WriteLine("  File Size: {0} {1}", Math.Floor((fmb < 1 ? fkb : fmb) * 10) / 10, (fmb < 1 ? "KB" : "MB"));
            Console.WriteLine("  Duration: {0:D2}:{1:D2}:{2:D2}", totalTime.Hours, totalTime.Minutes, totalTime.Seconds);
            Console.WriteLine("  Sample Rate: {0} Hz", waveFormat.SampleRate);
            Console.WriteLine("  Channels: {0} ch", waveFormat.Channels);
            Console.WriteLine("  Bit Rate: {0} kbps", waveFormat.AverageBytesPerSecond * 8 / 1000);
            Console.WriteLine();
        }
    }
}

