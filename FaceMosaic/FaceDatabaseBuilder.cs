using Emgu.CV;
using Emgu.CV.Structure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace FaceMosaic
{
    class FaceDatabaseBuilder
    {
        private CascadeClassifier m_classifier;

        public FaceDatabaseBuilder()
        {
        }

        static Image<Bgr, Byte> PanScan(Image<Bgr, Byte> image)
        {
            //string filename = "Tests\\9faces.jpg";
            //filename = "Tests\\Lena.jpg";
            //filename = "Tests\\faces.jpg";
            //filename = "Tests\\IMG_2777_color.jpg";

            //Image<Bgr, Byte> image = new Image<Bgr, byte>(filename);

            float source_width = image.Width;
            float source_height = image.Height;

            float target_width = 60;
            float target_height = 60;
            float target_aspect = target_width / target_height;

            float crop_width = source_width;
            float crop_height = source_width / target_aspect;

            if (crop_height > source_height)
            {
                crop_width *= (source_height / crop_height);
                crop_height = source_height;
            }

            Rectangle crop = new Rectangle(0, 0, (int)crop_width, (int)crop_height);
            crop.X = (int)((source_width - crop_width) / 2.0f);
            crop.Y = (int)((source_height - crop_height) / 2.0f);

            if (crop.Width > source_width) crop.Width = (int)source_width;
            if (crop.Height > source_height) crop.Height = (int)source_height;
            if (crop.X < 0) crop.X = 0;
            if (crop.Y < 0) crop.Y = 0;

            Image<Bgr, Byte> segment = image.Copy(crop);
            float scaler = target_width / (float)segment.Width;
            if (scaler < 1.0)
                segment = segment.Resize(scaler, Emgu.CV.CvEnum.INTER.CV_INTER_LANCZOS4);

            return segment;
        }

        public bool ProcessFile(string filename, string output)
        {
            Image<Bgr, Byte> image = new Image<Bgr, byte>(filename); //Read the files as an 8-bit Bgr image  
            if (image == null) return false;

            Stopwatch watch = Stopwatch.StartNew();
            Rectangle[] detected;
            using (Image<Gray, Byte> gray = image.Convert<Gray, Byte>()) //Convert it to Grayscale
            {
                //normalizes brightness and increases contrast of the image
                gray._EqualizeHist();

                //Detect the faces  from the gray scale image and store the locations as rectangle
                //The first dimensional is the channel
                //The second dimension is the index of the rectangle in the specific channel
                detected = m_classifier.DetectMultiScale(
                   gray,
                   1.1,
                   7,
                   new Size(80, 80),//20
                   Size.Empty);
            }

            watch.Stop();
            long detectionTime = watch.ElapsedMilliseconds;

            if ((detected == null) || detected.Length == 0) return true;

            foreach(Rectangle rectangle in detected)
            {
                Image<Bgr, Byte> cut = image.Copy(rectangle);

                cut = PanScan(cut);

                string id = Guid.NewGuid().ToString();

                cut.Save(output + id + ".jpg");
            }

            Log("ProcessFile detectionTime=" + detectionTime + " faces=" + detected.Length);

            return true;
        }

        void Log(string message)
        {
            System.Console.WriteLine(message);
        }

        void Clean()
        {
            List<string> targets = new List<string>();

            string[] scanned = null;

            scanned = System.IO.Directory.GetFiles(".", "*.jpg");
            if (scanned != null) targets.AddRange(scanned.ToList());

            scanned = System.IO.Directory.GetFiles(".", "*.jpeg");
            if (scanned != null) targets.AddRange(scanned.ToList());

            scanned = System.IO.Directory.GetFiles(".", "*.png");
            if (scanned != null) targets.AddRange(scanned.ToList());


            foreach (var current in targets)
            {
                try {
                    File.Delete(current);
                }
                catch { }
            }
        }

        public void Work(string source, string destination)
        {
            Log("Loading Classifier");
            m_classifier = new CascadeClassifier(System.IO.Path.Combine("haarcascades", "haarcascade_frontalface_default.xml"));
            if (m_classifier == null)
            {
                Log("Failed to create classifer");
                return;
            }
             

            Log("Scanning files " + source);
            string[] files = System.IO.Directory.GetFiles(source, "*.jpg");
            foreach (var current in files)
            {
                ProcessFile(current, destination);
                File.Delete(current);
            }

            m_classifier.Dispose();
        }
    }
}
