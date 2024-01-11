using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Drawing;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AForge.Video;
using AForge.Video.DirectShow;
using Emgu.CV;
using Emgu.CV.Structure;
using System.Diagnostics;
using Emgu.CV.Dai;
using System.Linq;
using System.Windows.Input;

namespace FrenzelWPFApp
{
    public partial class MainWindow : Window
    {
        private FilterInfoCollection videoDevices;
        private int leftVideoDeviceIdx = 0;
        private int rightVideoDeviceIdx = 0;

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

        private bool isCamerasChanged = false;

        string outputDirectory = @"C:\Users\Mustafa\Desktop\FrenzelRecords\";

        #region Parameters

        public VideoWriter videoWriter;
        public int codec1 = VideoWriter.Fourcc('H', '2', '6', '4');
        public int backend_idx = 1400;

        List<Mat> imageSequenceMats = new List<Mat>();




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
            btnEndDiagnosis.IsEnabled = false;
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

            //videoWriter = new VideoWriter(outputFileName, backend_idx, codec1, 25, new System.Drawing.Size(640, 480), false);
        }
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Video cihazlarını yükle
            videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (videoDevices.Count >= 2)
            {
                for (int i = 0; i < videoDevices.Count ; i++)
                {
                    if (videoDevices[i].Name.Contains("Integrated Camera"))
                    {
                        leftVideoDeviceIdx = i;
                        for (i = i+1; i < videoDevices.Count; i++)
                        {
                            if (videoDevices[i].Name.Contains("Integrated Camera"))
                            {
                                rightVideoDeviceIdx = i;

                            }
                        }
                        break;
                    }
                }

                if(leftVideoDeviceIdx != 0 && rightVideoDeviceIdx != 0)
                {
                    leftVideoSource = new VideoCaptureDevice(videoDevices[leftVideoDeviceIdx].MonikerString);
                    rightVideoSource = new VideoCaptureDevice(videoDevices[rightVideoDeviceIdx].MonikerString);
                    leftVideoSource.NewFrame += LeftVideoSource_NewFrame;
                    rightVideoSource.NewFrame += RightVideoSource_NewFrame;

                    leftVideoSource.Start();
                    rightVideoSource.Start();
                }
                else
                {
                    MessageBox.Show("Frenzel Gözlük bulunamadı lütfen bağlantıları kontrol ediniz.");
                }
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

        private void ChangeCameras_Click(object sender, RoutedEventArgs e)
        {
            if (isCamerasChanged)
            {
                isCamerasChanged = false;
                leftVideoSource.NewFrame -= RightVideoSource_NewFrame; 
                rightVideoSource.NewFrame -= LeftVideoSource_NewFrame;
                leftVideoSource.NewFrame += LeftVideoSource_NewFrame;
                rightVideoSource.NewFrame += RightVideoSource_NewFrame; 
            }
            else
            {
                isCamerasChanged = true;
                leftVideoSource.NewFrame -= LeftVideoSource_NewFrame;
                rightVideoSource.NewFrame -= RightVideoSource_NewFrame;
                leftVideoSource.NewFrame += RightVideoSource_NewFrame;
                rightVideoSource.NewFrame += LeftVideoSource_NewFrame;
            }
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
            string filePrefix = txtIdentityNumber.Text;

            if (filePrefix.Length > 0)
            {
                string[] matchingFiles = Directory.GetFiles(outputDirectory)
                .Where(file => Path.GetFileName(file).StartsWith(filePrefix))
                .ToArray();

                if (matchingFiles != null && matchingFiles.Length > 0)
                {
                    string firstId = Path.GetFileNameWithoutExtension(matchingFiles[0]).Substring(0,11);


                    if (matchingFiles.All(file => Path.GetFileName(file).StartsWith(firstId)))
                    {
                        var patientinfo = Path.GetFileNameWithoutExtension(matchingFiles[0]).Split('_');
                        txtIdentityNumber.Text = patientinfo[0];
                        txtLastName.Text = patientinfo[1];
                        txtFirstName.Text = patientinfo[2];
                        dpBirthDate.Text = patientinfo[3];
                        return;
                    }


                    MessageBox.Show("Birden çok eşleşme bulundu! Lütfen daha çok karakter giriniz.");

                    return;
                }
            }
            MessageBox.Show("Kayıt Bulunamadı!");
            
        }

        private void StartDiagnosis_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                

                if(txtIdentityNumber.Text.Length > 0 && txtFirstName.Text.Length > 0 && txtLastName.Text.Length > 0 && dpBirthDate.Text.Length > 0) 
                {
                    btnStartDiagnosis.IsEnabled = false;
                    string filePath = outputDirectory + txtIdentityNumber.Text + '_' + txtLastName.Text + '_' + txtFirstName.Text + '_' + dpBirthDate.Text + '_' + DateTime.Now.ToString("dd.MM.yyyy_HH.mm.ss.fff") + ".mp4";
                    // Kaydı başlatma
                    videoWriter = new VideoWriter(filePath, backend_idx, codec1, 10, new System.Drawing.Size(1280, 480), true);
                    isRecording = true;
                    recordingStopwatch.Reset();
                    recordingStopwatch.Start();
                    numofframes = 0;
                    numofframesleft = 0;
                    numofframesright = 0;
                    btnEndDiagnosis.IsEnabled = true;
                }
                else
                {
                    MessageBox.Show("Lütfen hasta bilgilerini giriniz.");
                }
            }
            catch (Exception)
            {

                throw;
            }
        }
        private void EndDiagnosis_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnEndDiagnosis.IsEnabled = false;
                // Kaydı durdurma işlemleri
                isRecording = false;
                videoWriter.Dispose();
                imageSequenceMats.Clear();
                Debug.WriteLine("number of frames = " + numofframes);
                Debug.WriteLine("number of frames left= " + numofframesleft);
                Debug.WriteLine("number of frames right= " + numofframesright);
                recordingStopwatch.Stop();
                TimeSpan recordingDuration = recordingStopwatch.Elapsed;
                Debug.WriteLine($"Recording duration: {recordingDuration}");
                btnStartDiagnosis.IsEnabled = true;
                
            }
            catch (Exception)
            {

                throw;
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }

        private void Window_Minimize(object sender, EventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Window_Maximize(object sender, EventArgs e)
        {
            if(this.WindowState == WindowState.Maximized)
                this.WindowState = WindowState.Normal;
            else
                this.WindowState = WindowState.Maximized;
        }
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
    }
}
