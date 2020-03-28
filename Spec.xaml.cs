using System.Windows.Controls;
using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Diagnostics;

using NAudio.Wave;

namespace SpecWpfMash
{
    /// <summary>
    /// Interaction logic for Test.xaml
    /// </summary>
    public partial class Spec : UserControl
     {

        private Spectrogram.Spectrogram spec;
        //System.Windows.Forms.PictureBox pictureBox1;

        public Spec()
        {
            InitializeComponent();

            //pictureBox1 = new System.Windows.Forms.PictureBox();
            //windowsFormsHost1.Child = pictureBox1;
         
            ScanSpeakerOutputs();
            ScanMicInputs();

            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(1);
            timer.Tick += new EventHandler(Timer1_Tick);
            timer.Start();

            PlotInitialize();

            /// spectogram ///
            cbDisplay.Items.Add("horizontal repeat");
            cbDisplay.Items.Add("waterfall");
            cbDisplay.SelectedItem = cbDisplay.Items[0];

            string[] colormapNames = Enum.GetNames(typeof(Spectrogram.Colormap));
            Array.Sort(colormapNames);
            cbColormap.ItemsSource = colormapNames;
            //cbColormap.Items.AddRange(colormapNames);
            cbColormap.SelectedItem = cbColormap.Items[cbColormap.Items.Count - 1];
        }

        private void ScanSpeakerOutputs()
        {
            cbDeviceSpk.Items.Clear();
            for (int i = 0; i < NAudio.Wave.WaveOut.DeviceCount; i++)
                cbDeviceSpk.Items.Add(NAudio.Wave.WaveOut.GetCapabilities(i).ProductName);
            if (cbDeviceSpk.Items.Count > 0)
                cbDeviceSpk.SelectedIndex = 0;
            else
                MessageBox.Show("ERROR: no recording devices available");
        }

        private void ScanMicInputs()
        {
            cbDeviceMic.Items.Clear();
            for (int i = 0; i < NAudio.Wave.WaveIn.DeviceCount; i++)
                cbDeviceMic.Items.Add(NAudio.Wave.WaveIn.GetCapabilities(i).ProductName);
            if (cbDeviceMic.Items.Count > 0)
                cbDeviceMic.SelectedIndex = 0;
            else
                MessageBox.Show("ERROR: no recording devices available");
        }

        private double[] pcmValues;
       // private double[] pcmValues2;
        private void PlotInitialize(int pointCount = 8_000 * 10)
        {
            pcmValues = new double[pointCount];
         //   pcmValues2 = new double[pointCount * 6];//
            graphTS.plt.Clear();
            graphTS.plt.PlotSignal(pcmValues, sampleRate: 8000, markerSize: 0);// color: Color.Blue);
           // graphTS.plt.PlotSignal(pcmValues2, sampleRate: 8000, markerSize: 0, color: System.Drawing.Color.Orange);
            graphTS.plt.PlotVLine(0, color: System.Drawing.Color.Red, lineWidth: 2);
            graphTS.plt.PlotHLine(0, color: System.Drawing.Color.Black, lineWidth: 1);
            graphTS.plt.YLabel("Amplitude (%)");
            graphTS.plt.XLabel("Time (s)");
            graphTS.plt.Style(ScottPlot.Style.Light1);
            graphTS.Render();
            if (dataFft != null)
            {
                graphFFT.plt.Clear();
                double fftSpacing = (double)wvin.WaveFormat.SampleRate / dataFft.Length;
                graphFFT.plt.PlotSignal(dataFft, sampleRate: fftSpacing, markerSize: 0);
                graphFFT.plt.PlotHLine(0, color: System.Drawing.Color.Black, lineWidth: 1);
                graphFFT.plt.YLabel("Power");
                graphFFT.plt.XLabel("Frequency (kHz)");
                graphFFT.plt.Style(ScottPlot.Style.Light1);
                graphFFT.Render();
            }
        }

        private NAudio.Wave.WaveInEvent wvin;
        private NAudio.Wave.WaveOutEvent wvout;
        private NAudio.Wave.AudioFileReader audioFile;
        NAudio.Wave.WaveFileWriter writer = null; // rec
        private int buffersRead = 0;
        Int16[] dataPcm;
        private void OnDataAvailable(object sender, NAudio.Wave.WaveInEventArgs args)
        {
            writer.Write(args.Buffer, 0, args.BytesRecorded); // rec
            int bytesPerSample = wvin.WaveFormat.BitsPerSample / 8;
            int samplesRecorded = args.BytesRecorded / bytesPerSample;
            int buffersToDisplay = 80_000 / samplesRecorded;
            int offset = (buffersRead % buffersToDisplay) * samplesRecorded;
            for (int i = 0; i < samplesRecorded; i++)
            {
                pcmValues[i + offset] = BitConverter.ToInt16(args.Buffer, i * bytesPerSample);
                //pcmValues2[i + offset] = pcmValues[i + offset] - 1.0;
            }
            //
            if (dataPcm == null)
                dataPcm = new Int16[samplesRecorded];
            for (int i = 0; i < samplesRecorded; i++)
                dataPcm[i] = BitConverter.ToInt16(args.Buffer, i * bytesPerSample);
            //
            buffersRead += 1;


            // TODO: make this sane
            ScottPlot.PlottableVLine axLine = (ScottPlot.PlottableVLine)graphTS.plt.GetPlottables()[1]; // 1..   // PlottableAxLine -> PlottableVLine
            axLine.position = offset / 8000.0;

            ////// Spectrogram 
            float[] buffer = new float[args.BytesRecorded / bytesPerSample];
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = BitConverter.ToInt16(args.Buffer, i * bytesPerSample);
            try
            {
                if (waterfall)
                    spec.AddScroll(buffer, fixedSize: 320);// (int)pictureBox1.Height); //(int)
                else
                    spec.AddCircular(buffer, fixedSize: 840);// (int)pictureBox1.Width);
                renderNeeded = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("EXCEPTION: " + ex);
            }
        }

        private void AudioMonitorInitialize(int DeviceIndex,
                int sampleRate = 44100,//8000  // higher-> height lower
                int bitRate = 16,
                int channels = 2,
                int bufferMilliseconds = 20, // higher -> faster speed only affects speed
                bool start = true,
                int fftSize = 2048,//1024 2 power higher -> higher resolution res Hz goes down  // lower-> height lower
                int step = 1250//250 higher -> lower speed only affects speed
            )
        {
            if (wvin == null)
            {
                spec = new Spectrogram.Spectrogram(sampleRate, fftSize, step);

                wvin = new NAudio.Wave.WaveInEvent();
                wvin.DeviceNumber = DeviceIndex;
                wvin.WaveFormat = new NAudio.Wave.WaveFormat(sampleRate, bitRate, channels);
                wvin.DataAvailable += OnDataAvailable;
                wvin.BufferMilliseconds = bufferMilliseconds;

                //// Recording
                var outputFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "NAudio");
                Directory.CreateDirectory(outputFolder);
                var outputFilePath = System.IO.Path.Combine(outputFolder, "recorded.wav");
                writer = new WaveFileWriter(outputFilePath, wvin.WaveFormat);
                ////
                if (start)
                    wvin.StartRecording();
            }
        }

        //
        double[] dataFft;

        private void updateFFT()
        {
            // the PCM size to be analyzed with FFT must be a power of 2
            int fftPoints = 2;
            while (fftPoints * 2 <= dataPcm.Length)
                fftPoints *= 2;

            // apply a Hamming window function as we load the FFT array then calculate the FFT
            NAudio.Dsp.Complex[] fftFull = new NAudio.Dsp.Complex[fftPoints];
            for (int i = 0; i < fftPoints; i++)
                fftFull[i].X = (float)(dataPcm[i] * NAudio.Dsp.FastFourierTransform.HammingWindow(i, fftPoints));
            NAudio.Dsp.FastFourierTransform.FFT(true, (int)Math.Log(fftPoints, 2.0), fftFull);

            // copy the complex values into the double array that will be plotted
            if (dataFft == null)
                dataFft = new double[fftPoints / 2];
            for (int i = 0; i < fftPoints / 2; i++)
            {
                double fftLeft = Math.Abs(fftFull[i].X + fftFull[i].Y);
                double fftRight = Math.Abs(fftFull[fftPoints - i - 1].X + fftFull[fftPoints - i - 1].Y);
                dataFft[i] = fftLeft + fftRight;
            }
        }
        //

        //private NAudio.Wave.WaveFileReader wave = null;
        //private NAudio.Wave.DirectSoundOut output = null;
        private String audioFilePath = null;
        private void BtnFind_Click(object sender, EventArgs e)
        {
            Microsoft.Win32.OpenFileDialog open = new Microsoft.Win32.OpenFileDialog(); // Microsoft.Win32 더해줌
            open.Filter = "Audio File (*.wav;*.mp3)|*.mp3;*.wav;";
            if (open.ShowDialog() != true) return;
             audioFilePath = open.FileName;
            //wave = new NAudio.Wave.WaveFileReader(open.FileName);

        }

        private void BtnStart_Click(object sender, EventArgs e)
        {
            AudioMonitorInitialize(cbDeviceMic.SelectedIndex);
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            // rec
            writer?.Dispose();
            writer = null;
            //

            if (wvin != null)
            {
                wvin.StopRecording();
                wvin = null;
            }

        }

        private void BtnStartSound_Click(object sender, EventArgs e)
        {
            wvout = new NAudio.Wave.WaveOutEvent();
            //WaveStream pcm = null;
            //audioFile = new NAudio.Wave.AudioFileReader(@"C:\Users\user\Desktop\test.mp3"); // set your sound file path FIXED
            if (audioFilePath != null)
            {
                audioFile = new NAudio.Wave.AudioFileReader(audioFilePath);
                //pcm = WaveFormatConversionStream.CreatePcmStream(audioFile);
            }
            if (audioFile != null)
            {
                wvout.DeviceNumber = cbDeviceSpk.SelectedIndex;  // pre-Init
                
                wvout.Init(audioFile);
                Debug.WriteLine("wvout Devicenumber {0}", wvout.DeviceNumber);
                wvout.Play();

                // If you wanna play in 2 speakers  
                //wvout2.DeviceNumber = 0;
                //wvout2.Init(audioFile2);

                //wvout2.Play();
                
                //NAudio.Wave.WaveChannel32 wave = new NAudio.Wave.WaveChannel32(audioFile);
                //byte[] buffer = new byte[16384];
                //int read = 0;
                //while (wave.Position < wave.Length)
                //{ 
                //    read = wave.Read(buffer, 0, 16384);
                //    for (int i = 0; i < read / 4; i++)
                //    {
                //        pcmValues2[i] = BitConverter.ToDouble(buffer, i * 4);
                //        Debug.WriteLine("i : {0} read : {1} pcmValues2[i] : {2:0.0000000}\n", i, read, pcmValues2[i]);
                //    }
                //}
            }
        }

        private void BtnStopSound_Click(object sender, EventArgs e)
        {
            wvout.Dispose();
            wvout = null;
            audioFile.Dispose();
            audioFile = null;
        }

        bool renderNeeded = false;//spg
        bool busyRendering = false;//spg
        private void Timer1_Tick(object sender, EventArgs e)
        {
            //
            if (dataPcm == null)
                return;

            updateFFT();

            if (graphFFT.plt.GetPlottables().Count == 0)
                PlotInitialize();
            //

            if (cbAutoAxis.IsChecked ?? true)
            {
                graphTS.plt.AxisAuto();
                graphTS.plt.TightenLayout();
            }
            graphTS.Render();
            if (cbAutoAxis2.IsChecked ?? true)
            {
                graphFFT.plt.AxisAuto();
                graphFFT.plt.TightenLayout();
            }
            graphFFT.Render();


            // spg //
            if (!renderNeeded)
                return;

            if ((spec == null) || (spec.fftList.Count == 0))
                return;

            if (busyRendering)
                return;
            else
                busyRendering = true;

            Spectrogram.Colormap colormap = (Spectrogram.Colormap)Enum.Parse(typeof(Spectrogram.Colormap), cbColormap.Text);

            spectrogramImage.Source = loadBitmap(spec.GetBitmap(
                intensity: (float)tbIntensity.Value,
                decibels: cbDecibels.IsChecked ?? false,
                vertical: waterfall,
                colormap: colormap,
                showTicks: cbTicks.IsChecked ?? false,
                highlightLatestColumn: (cbDisplay.Text != "waterfall"),
                freqHigh: 8000,
                freqLow: 0,
                tickSpacingHz: 1000, // 4000,0,250
                tickSpacingSec: 1
                ));

            //pictureBox1.BackgroundImage = spec.GetBitmap(
            //    intensity: (float)tbIntensity.Value,
            //    decibels: cbDecibels.IsChecked ?? false,
            //    vertical: waterfall,
            //    colormap: colormap,
            //    showTicks: cbTicks.IsChecked ?? false,
            //    highlightLatestColumn: (cbDisplay.Text != "waterfall"),
            //    freqHigh:12000,
            //    freqLow:0,
            //    tickSpacingHz: 1000, // 4000,0,250
            //    tickSpacingSec: 1
            //    );


            //   lblStatus.Text = $"spectrogram contains {spec.fftList.Count} FFT samples | last render: {spec.GetLastRenderTime()} ms";
            renderNeeded = false;
            busyRendering = false;
            ///
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        static extern int DeleteObject(IntPtr o);
        public static BitmapSource loadBitmap(System.Drawing.Bitmap source) { 
            IntPtr ip = source.GetHbitmap(); 
            BitmapSource bs = null; 
            try { 
                bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(ip, IntPtr.Zero, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions()); 
            } finally {
                DeleteObject(ip);
            } 
            return bs;
        }


        public static BitmapSource ConvertBitmap(System.Drawing.Bitmap source)
        {
            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                          source.GetHbitmap(),
                          IntPtr.Zero,
                          Int32Rect.Empty,
                          System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
        }

        BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                return bitmapimage;
            }
        }

        private void tbIntensity_Scroll(object sender, EventArgs e)
        {
            //   tbIntensity.Value;
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            MessageBox.Show(spec.GetFftInfo(), "Configuration Details");
        }

        public bool waterfall = false;
        private void cbDisplay_SelectedIndexChanged(object sender, EventArgs e)
        {
            waterfall = (cbDisplay.Text == "waterfall");
        }
    }
}
