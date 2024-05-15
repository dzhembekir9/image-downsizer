using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ImageDownsizer
{
    public partial class Form1 : Form
    {
        private Button btnSelectImage;
        private TextBox txtDownscaleFactor;
        private PictureBox pictureBoxOriginal;
        private PictureBox pictureBoxDownscaled;
        private Button btnDownscale;
        private Button btnSaveDownscaled;

        private Label lblSequentialTime;
        private Label lblParallelTime;
        private Label lblSpeedup;

        public Form1()
        {
            InitializeComponent();
            InitializeFormControls();
            this.Size = new Size(900, 550);
        }

        private void InitializeFormControls()
        {
            btnSelectImage = new Button
            {
                Text = "Select Image",
                Location = new Point(10, 10),
                Size = new Size(100, 30)
            };
            btnSelectImage.Click += btnSelectImage_Click;

            txtDownscaleFactor = new TextBox
            {
                Location = new Point(120, 10),
                Size = new Size(100, 30)
            };

            btnDownscale = new Button
            {
                Text = "Downscale",
                Location = new Point(230, 10),
                Size = new Size(100, 30)
            };
            btnDownscale.Click += btnDownscale_Click;

            pictureBoxOriginal = new PictureBox
            {
                Location = new Point(10, 50),
                Size = new Size(300, 300),
                BorderStyle = BorderStyle.FixedSingle
            };

            pictureBoxDownscaled = new PictureBox
            {
                Location = new Point(320, 50),
                Size = new Size(300, 300),
                BorderStyle = BorderStyle.FixedSingle
            };

            btnSaveDownscaled = new Button
            {
                Text = "Save Downscaled Image",
                Location = new Point(10, 360),
                Size = new Size(150, 30)
            };

            lblSequentialTime = new Label
            {
                Text = "Sequential Time: ",
                Location = new Point(10, 400),
                Size = new Size(300, 30),
                AutoSize = true
            };

            lblParallelTime = new Label
            {
                Text = "Parallel Time: ",
                Location = new Point(10, 430),
                Size = new Size(300, 30),
                AutoSize = true
            };

            lblSpeedup = new Label
            {
                Text = "Speedup: ",
                Location = new Point(10, 460),
                Size = new Size(300, 30),
                AutoSize = true
            };

            Controls.Add(btnSelectImage);
            Controls.Add(txtDownscaleFactor);
            Controls.Add(btnDownscale);
            Controls.Add(pictureBoxOriginal);
            Controls.Add(pictureBoxDownscaled);
            Controls.Add(lblSequentialTime);
            Controls.Add(lblParallelTime);
            Controls.Add(lblSpeedup);

            btnSaveDownscaled.Click += btnSaveDownscaled_Click;
            Controls.Add(btnSaveDownscaled);
        }

        private void btnSelectImage_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "Open Image";
                dlg.Filter = "Image files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp";

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    pictureBoxOriginal.Image = new Bitmap(dlg.FileName);
                }
            }
        }

        private void btnDownscale_Click(object sender, EventArgs e)
        {
            if (pictureBoxOriginal.Image == null)
            {
                MessageBox.Show("Please select an image first.");
                return;
            }

            if (!double.TryParse(txtDownscaleFactor.Text, out double scalePercentage) || scalePercentage <= 0 || scalePercentage > 100)
            {
                MessageBox.Show("Enter a valid downscaling factor (a number greater than 0 and up to 100).");
                return;
            }

            double scale = scalePercentage / 100.0;

            Bitmap original = new Bitmap(pictureBoxOriginal.Image);

            // Sequential Downscaling
            Stopwatch stopwatch = Stopwatch.StartNew();
            Bitmap downscaledSequential = DownscaleImageSequential(original, scale);
            stopwatch.Stop();
            long sequentialTime = stopwatch.ElapsedMilliseconds;
            lblSequentialTime.Text = $"Sequential Time: {sequentialTime} ms";

            // Parallel Downscaling
            stopwatch.Restart();
            Bitmap downscaledParallel = DownscaleImageParallel(original, scale); // Assuming this is the parallel method
            stopwatch.Stop();
            long parallelTime = stopwatch.ElapsedMilliseconds;
            lblParallelTime.Text = $"Parallel Time: {parallelTime} ms";

            // Update the UI with the parallel downscaled image
            pictureBoxDownscaled.Image = downscaledParallel;

            // Calculate and display speedup
            double speedup = sequentialTime / (double)parallelTime;
            lblSpeedup.Text = $"Speedup: {speedup:F2}x";
        }


        private Bitmap DownscaleImageParallel(Bitmap original, double scale)
        {
            int newWidth = (int)(original.Width * scale);
            int newHeight = (int)(original.Height * scale);
            Bitmap downscaled = new Bitmap(newWidth, newHeight, original.PixelFormat);

            // Lock the bits for both original and downscaled images
            BitmapData originalData = original.LockBits(new Rectangle(0, 0, original.Width, original.Height), ImageLockMode.ReadOnly, original.PixelFormat);
            BitmapData downscaledData = downscaled.LockBits(new Rectangle(0, 0, newWidth, newHeight), ImageLockMode.WriteOnly, downscaled.PixelFormat);

            int bytesPerPixel = Image.GetPixelFormatSize(original.PixelFormat) / 8;
            int processorCount = Environment.ProcessorCount;
            int rowsPerThread = newHeight / processorCount;
            var threads = new List<Thread>();

            for (int i = 0; i < processorCount; i++)
            {
                int startRow = i * rowsPerThread;
                int endRow = (i == processorCount - 1) ? newHeight : startRow + rowsPerThread;

                Thread thread = new Thread(() =>
                {
                    DownscalePortion(originalData, downscaledData, startRow, endRow, scale, bytesPerPixel);
                });
                threads.Add(thread);
                thread.Start();
            }

            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            original.UnlockBits(originalData);
            downscaled.UnlockBits(downscaledData);

            return downscaled;
        }

        private void DownscalePortion(BitmapData originalData, BitmapData downscaledData, int startRow, int endRow, double scale, int bytesPerPixel)
        {
            IntPtr originalScan0 = originalData.Scan0;
            IntPtr downscaledScan0 = downscaledData.Scan0;

            int originalStride = originalData.Stride;
            int downscaledStride = downscaledData.Stride;

            byte[] originalPixel = new byte[bytesPerPixel];

            for (int y = startRow; y < endRow; y++)
            {
                for (int x = 0; x < downscaledData.Width; x++)
                {
                    int originalX = (int)(x / scale);
                    int originalY = (int)(y / scale);

                    long originalIndex = originalY * originalStride + originalX * bytesPerPixel;
                    long downscaledIndex = y * downscaledStride + x * bytesPerPixel;

                    // Copy the pixel data
                    Marshal.Copy(IntPtr.Add(originalScan0, (int)originalIndex), originalPixel, 0, bytesPerPixel);
                    Marshal.Copy(originalPixel, 0, IntPtr.Add(downscaledScan0, (int)downscaledIndex), bytesPerPixel);
                }
            }
        }

        private Bitmap DownscaleImageSequential(Bitmap original, double scale)
        {
            int newWidth = (int)(original.Width * scale);
            int newHeight = (int)(original.Height * scale);
            Bitmap downscaled = new Bitmap(newWidth, newHeight, original.PixelFormat);

            // Lock the bits for both original and downscaled images
            BitmapData originalData = original.LockBits(new Rectangle(0, 0, original.Width, original.Height), ImageLockMode.ReadOnly, original.PixelFormat);
            BitmapData downscaledData = downscaled.LockBits(new Rectangle(0, 0, newWidth, newHeight), ImageLockMode.WriteOnly, downscaled.PixelFormat);

            int bytesPerPixel = Image.GetPixelFormatSize(original.PixelFormat) / 8;

            // Pointer to the first pixel of the original and downscaled images
            IntPtr originalPtr = originalData.Scan0;
            IntPtr downscaledPtr = downscaledData.Scan0;

            // Bytes array to hold pixel data
            byte[] originalPixels = new byte[originalData.Stride * original.Height];
            byte[] downscaledPixels = new byte[downscaledData.Stride * newHeight];

            // Copy the RGB values into the array
            Marshal.Copy(originalPtr, originalPixels, 0, originalPixels.Length);

            for (int y = 0; y < newHeight; y++)
            {
                for (int x = 0; x < newWidth; x++)
                {
                    int originalX = (int)(x / scale);
                    int originalY = (int)(y / scale);

                    for (int byteIndex = 0; byteIndex < bytesPerPixel; byteIndex++)
                    {
                        // Calculate the index of the byte in the original and downscaled images
                        int originalByteIndex = originalY * originalData.Stride + originalX * bytesPerPixel + byteIndex;
                        int downscaledByteIndex = y * downscaledData.Stride + x * bytesPerPixel + byteIndex;

                        // Copy the pixel byte from the original to the downscaled image
                        downscaledPixels[downscaledByteIndex] = originalPixels[originalByteIndex];
                    }
                }
            }

            // Copy modified bytes back to the bitmap
            Marshal.Copy(downscaledPixels, 0, downscaledPtr, downscaledPixels.Length);

            // Unlock the bits
            original.UnlockBits(originalData);
            downscaled.UnlockBits(downscaledData);

            return downscaled;
        }

        private void btnSaveDownscaled_Click(object sender, EventArgs e)
        {
            if (pictureBoxDownscaled.Image == null)
            {
                MessageBox.Show("There is no downscaled image to save. Please create one first.");
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Title = "Save Image";
                sfd.Filter = "JPEG Image|*.jpg|Bitmap Image|*.bmp|GIF Image|*.gif|PNG Image|*.png";
                sfd.FileName = "downscaled_image";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    ImageFormat format = ImageFormat.Png;
                    switch (sfd.FilterIndex)
                    {
                        case 1:
                            format = ImageFormat.Jpeg;
                            break;
                        case 2:
                            format = ImageFormat.Bmp;
                            break;
                        case 3:
                            format = ImageFormat.Gif;
                            break;
                    }

                    pictureBoxDownscaled.Image.Save(sfd.FileName, format);
                }
            }
        }

    }
}
