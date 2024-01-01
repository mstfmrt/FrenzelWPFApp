using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Drawing;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AForge.Video;
using AForge.Video.DirectShow;
using Emgu.CV;
using Emgu.CV.Structure;
using System.Diagnostics;

namespace FrenzelWPFApp
{
    public partial class MainWindow : Window
    {
        private FilterInfoCollection videoDevices;
        private VideoCaptureDevice leftVideoSource;
        private VideoCaptureDevice rightVideoSource;
        private int actualFrameRate;
        private Stopwatch recordingStopwatch = new Stopwatch();
        private Stopwatch frameCountStopwatch = new Stopwatch();
        private int leftFrameCount = 0;
        private int rightFrameCount = 0;
        private List<Mat> leftFrames = new List<Mat>();
        private List<Mat> rightFrames = new List<Mat>();

        public int numofframes = 0;
        public int numofframesright = 0;
        public int numofframesleft = 0;
        private DispatcherTimer frameRateTimer;


        #region Parameters

        public VideoWriter videoWriter;
        public int codec1 = VideoWriter.Fourcc('H', '2', '6', '4');
        public int backend_idx = 1400;

        List<Mat> imageSequenceMats = new List<Mat>();

        string outputFileName = @"C:\Users\Mustafa\Desktop\output.mp4";


        #endregion

        private bool isRecording = false;

        public MainWindow()
        {
            InitializeComponent();
            //InitializeVariables();
            frameRateTimer = new DispatcherTimer();
            frameRateTimer.Interval = TimeSpan.FromSeconds(10);
            frameRateTimer.Tick += FrameRateTimer_Tick;
            frameRateTimer.Start();
            Loaded += MainWindow_Loaded;
        }


        private void FrameRateTimer_Tick(object sender, EventArgs e)
        {
            // Timer ticked every second
            double elapsedSeconds = frameCountStopwatch.Elapsed.TotalSeconds;
            Debug.WriteLine($"Left camera frames per second: {leftFrameCount}");
            Debug.WriteLine($"Right camera frames per second: {rightFrameCount}");
            Debug.WriteLine("");
            Debug.WriteLine("");
            // Reset frame counts
            leftFrameCount = 0;
            rightFrameCount = 0;
            frameCountStopwatch.Restart();
        }


        public void InitializeVariables()
        {
            Backend[] backends = CvInvoke.WriterBackends;

            videoWriter = new VideoWriter(outputFileName, backend_idx, codec1, 25, new System.Drawing.Size(640, 480), false);
        }
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Video cihazlarını yükle
            videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (videoDevices.Count >= 2)
            {
                leftVideoSource = new VideoCaptureDevice(videoDevices[3].MonikerString);
                rightVideoSource = new VideoCaptureDevice(videoDevices[4].MonikerString);
                leftVideoSource.NewFrame += LeftVideoSource_NewFrame;
                rightVideoSource.NewFrame += RightVideoSource_NewFrame;

                leftVideoSource.Start();
                rightVideoSource.Start();

                actualFrameRate = (int)leftVideoSource.VideoCapabilities[0].AverageFrameRate;
            }
            else
            {
                MessageBox.Show("En az iki video cihazı gerekir.");
                Close();
            }
        }

        private void LeftVideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            Dispatcher.Invoke(() =>
            {
                BitmapImage leftBitmapImage = ConvertToBitmapImage(eventArgs.Frame);
                leftEyeImage.Source = leftBitmapImage;
                leftFrameCount++;

                if (isRecording)
                {

                    // Convert leftBitmapImage to Mat
                    Mat leftMat = BitmapImageToMat(leftBitmapImage);

                    // Add the left frame to the FIFO array
                    leftFrames.Add(leftMat);

                    // Process frames if there are frames from both cameras
                    ProcessFrames();
                }
            });
        }

        private void RightVideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            Dispatcher.Invoke(() =>
            {
                BitmapImage rightBitmapImage = ConvertToBitmapImage(eventArgs.Frame);
                rightEyeImage.Source = rightBitmapImage;
                rightFrameCount++;

                if (isRecording)
                {
                    // Convert rightBitmapImage to Mat
                    Mat rightMat = BitmapImageToMat(rightBitmapImage);

                    // Add the right frame to the FIFO array
                    rightFrames.Add(rightMat);

                    // Process frames if there are frames from both cameras
                    ProcessFrames();

                    //Mat mat = BitmapImageToMat(bitmapImage);
                    //videoWriter.Write(mat);
                    //mat.Dispose();
                    numofframes++;
                    numofframesright++;
                }
            });
        }

        private BitmapImage ConvertToBitmapImage(System.Drawing.Bitmap bitmap)
        {
            BitmapImage bitmapImage = new BitmapImage();

            using (MemoryStream memory = new MemoryStream())
            {
                // Save the bitmap to the memory stream on the UI thread
                Dispatcher.Invoke(() => bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp));

                memory.Position = 0;

                // Load the bitmap from the memory stream on the UI thread
                Dispatcher.Invoke(() =>
                {
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = memory;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();
                });
            }

            return bitmapImage;
        }

        private Mat BitmapImageToMat(BitmapImage bitmapImage)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                BitmapEncoder encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapImage));
                encoder.Save(memory);

                // Decode the bitmap from memory stream
                using (Bitmap bmp = new Bitmap(memory))
                {
                    Image<Bgr, byte> image = bmp.ToImage<Bgr, byte>();
                    return image.Mat.Clone();
                }
            }
        }

        private void ProcessFrames()
        {
            // Check if there are frames from both cameras
            if (leftFrames.Count > 0 && rightFrames.Count > 0)
            {
                // Dequeue the oldest frames from both cameras
                Mat leftMat = leftFrames[0];
                Mat rightMat = rightFrames[0];

                // Remove the processed frames from the FIFO array
                leftFrames.RemoveAt(0);
                rightFrames.RemoveAt(0);

                // Combine left and right frames and display the result
                CombineAndDisplayFrames(leftMat, rightMat);
            }
        }

        private void CombineAndDisplayFrames(Mat leftMat, Mat rightMat)
        {
            // Combine left and right frames into a single frame (side by side)
            Mat combinedFrame = new Mat();
            CvInvoke.HConcat(leftMat, rightMat, combinedFrame);

            // Display the combined frame
            DisplayCombinedFrame(combinedFrame);
        }

        private void DisplayCombinedFrame(Mat combinedFrame)
        {
            // Convert the combined frame to BitmapImage and display it
            //BitmapImage combinedBitmapImage = MatToBitmapImage(combinedFrame);
            //combinedImage.Source = combinedBitmapImage;

            //Mat mat = BitmapImageToMat(combinedFrame);
            videoWriter.Write(combinedFrame);
            //mat.Dispose();
            // Increment the combined frame count
            //combinedFrameCount++;

            // Process the combined frame further or write it to a video if needed
            // ...
        }

        private void SearchPatient_Click(object sender, RoutedEventArgs e)
        {
            // Hasta arama işlemleri
            // Buraya hastanın kimlik numarasına göre arama kodunu ekleyebilirsiniz
        }

        private void StartDiagnosis_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (isRecording)
                {
                    // Kaydı durdurma işlemleri
                    isRecording = false;
                    btnStartDiagnosis.Content = "Start Diagnosis";
                    videoWriter.Dispose();
                    imageSequenceMats.Clear();
                    Debug.WriteLine("number of frames = " + numofframes);
                    Debug.WriteLine("number of frames left= " + numofframesleft);
                    Debug.WriteLine("number of frames right= " + numofframesright);
                    recordingStopwatch.Stop();
                    TimeSpan recordingDuration = recordingStopwatch.Elapsed;
                    Debug.WriteLine($"Recording duration: {recordingDuration}");
                }
                else
                {
                    // Kaydı başlatma
                    videoWriter = new VideoWriter(outputFileName, backend_idx, codec1, 10, new System.Drawing.Size(1280, 480), true);
                    isRecording = true;
                    btnStartDiagnosis.Content = "End Diagnosis";
                    recordingStopwatch.Reset();
                    recordingStopwatch.Start();
                    numofframes = 0;
                    numofframesleft = 0;
                    numofframesright = 0;
                }
            }
            catch (Exception)
            {

                throw;
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            // Uygulama kapatıldığında video kaynaklarını serbest bırak
            if (leftVideoSource != null && leftVideoSource.IsRunning)
                leftVideoSource.Stop();

            if (rightVideoSource != null && rightVideoSource.IsRunning)
                rightVideoSource.Stop();
        }
    }
}
