using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace FaceMosaic
{
    public class Record
    {
        public Image<Bgr, Byte> image;
        public Bgr average;
        public bool used = false;
    }

    public class DatabaseMatcher
    {
        List<Record> m_records = new List<Record>();
        Image<Bgr, Byte> m_image;
        Image<Gray, Byte> m_mask;
        int m_rows;
        int m_cols;
        int m_sizex;
        int m_sizey;
        int m_used;

        void Log(string message)
        {
            System.Console.WriteLine(message);
        }


        Record FindBestMatchTemplateMatch(Image<Bgr, Byte> template)
        {
            Record record = null;

            Emgu.CV.CvEnum.TM_TYPE method = Emgu.CV.CvEnum.TM_TYPE.CV_TM_CCORR;
            double match = 0.0;

            foreach (var current in m_records)
            {
                Image<Gray, float> result = current.image.MatchTemplate(template, method);


                double[] min, max;
                Point[] point1, point2;
                result.MinMax(out min, out max, out point1, out point2);

                if (record == null)
                {
                    if (method == Emgu.CV.CvEnum.TM_TYPE.CV_TM_SQDIFF || method == Emgu.CV.CvEnum.TM_TYPE.CV_TM_SQDIFF_NORMED)
                        match = min[0];
                    else
                        match = max[0];

                    record = current;
                }
                else
                {
                    /// For SQDIFF and SQDIFF_NORMED, the best matches are lower values. For all the other methods, the higher the better
                    if (method == Emgu.CV.CvEnum.TM_TYPE.CV_TM_SQDIFF || method == Emgu.CV.CvEnum.TM_TYPE.CV_TM_SQDIFF_NORMED)
                    {
                        if (min[0] < match)
                        {
                            match = min[0];
                            record = current;
                        }
                    }
                    else
                    {
                        if (max[0] > match)
                        {
                            match = max[0];
                            record = current;
                        }
                    }
                }
            }

            return record;
        }


        Record FindBestMatch(Bgr color)
        {
            double best = Double.MaxValue;
            Record record = null;

            foreach (var current in m_records)
            {
                if (current.used) continue;

                double x = current.average.Red - color.Red;
                double y = current.average.Green - color.Green;
                double z = current.average.Blue - color.Blue;
                double distance = Math.Sqrt(x * x + y * y + z * z);

                if (distance < best)
                {
                    record = current;
                    best = distance;
                }

            }

            return record;
        }

        Image<Bgr, Byte> Blend(Image<Bgr, Byte> A, Image<Bgr, Byte> B, Image<Gray, Byte> M)
        {
            //return B;

            Image<Bgr, Byte> output = new Image<Bgr, byte>(A.Width, A.Height);


            for (int x = 0; x != m_sizex; ++x)
                for (int y = 0; y != m_sizey; ++y)
                {
                    Bgr acolor = A[x, y];
                    Bgr bcolor = B[x, y];
                    double factor = M[x, y].Intensity / 255.0;

                    output[x, y] = new Bgr(acolor.Blue * factor + bcolor.Blue * (1.0 - factor),
                                           acolor.Green * factor + bcolor.Green * (1.0 - factor),
                                           acolor.Red * factor + bcolor.Red * (1.0 - factor));
                }


            return output;
        }

        void ProcessCell(int r, int c)
        {
            if (m_used == m_records.Count) {
                m_used = 0;

                foreach (var current in m_records)
                    current.used = false;
            }


            int cellx = c * m_sizex;
            int celly = r * m_sizey;
            Rectangle rect = new Rectangle();
            rect.X = cellx;
            rect.Y = celly;
            rect.Width = m_sizex;
            rect.Height = m_sizey;

            Image<Bgr, Byte> patch = m_image.Copy(rect);

//          Record found = FindBestMatchTemplateMatch (patch);

            Bgr average = patch.GetAverage();
            Record found = FindBestMatch(average);

            Image<Bgr, Byte> blended = Blend(patch, found.image, m_mask);


            m_image.ROI = rect;
            blended.CopyTo(m_image);
            m_image.ROI = Rectangle.Empty;

            //m_image.Save("dump.bmp");

            //image.ROI = face;
            //Image<Bgr, Byte> blur = image.SmoothBlur(10, 10);
            //blur.CopyTo(image);
            //image.ROI = Rectangle.Empty;
            //image.Draw(face, result.DebugColor, 2);

            found.used = true;
            ++m_used;

        }

        void Load(string filename)
        {
            Record record = new Record();
            record.image = new Image<Bgr, byte>(filename);
            if (record.image != null)
            {
                //
                // force 60x60
                //
                record.image = record.image.Resize(60, 60, Emgu.CV.CvEnum.INTER.CV_INTER_LANCZOS4);
                record.average = record.image.GetAverage();


               
                //record.template = record.image.Convert<Gray, Byte>();

                m_sizex = record.image.Width;
                m_sizey = record.image.Height;
                m_records.Add(record);
            }
        }
        public void Work(string database, string source)
        {
            m_image = new Image<Bgr, byte>(source);
            if (m_image == null)
            {
                Log("invalid source " + source);
                return;
            }
            
            

            Log("Scanning files " + database);
            string[] files = System.IO.Directory.GetFiles(database, "*.jpg");
            foreach (var current in files)
            {
                Load(current);
            }
            Log("Scanning done");

            m_rows = m_image.Height / m_sizey;
            m_cols = m_image.Width / m_sizex;

            m_used = 0;

            m_mask = new Image<Gray, Byte>(m_sizex, m_sizey);
            float halfx = m_sizex / 2;
            float halfy = m_sizey / 2;
            Ellipse elipse = new Ellipse(new PointF(halfx, halfy), new SizeF((float)m_sizex * 0.95f, (float)m_sizey * 0.95f), 90.0f);
            m_mask.Draw(elipse, new Gray(255), -1);
            m_mask._SmoothGaussian(15);
            m_mask = m_mask * 0.90;
            //ImageViewer.Show(m_mask, "Mask");


            //m_mask = new Image<Gray, Byte>(m_sizex, m_sizey);
            //float sx = (int)(m_sizex * 0.9f);
            //float sy = (int)(m_sizey * 0.9f);
            //Rectangle rect = new Rectangle((int)(m_sizex - sx) / 2, (int)(m_sizey - sy) / 2, (int)sx, (int)sy);
            //m_mask.Draw(rect, new Gray(255), -1);
            //m_mask._SmoothGaussian(15);
            //m_mask = m_mask * 0.75;
            //ImageViewer.Show(m_mask, "Mask");
            
            m_mask._Not();


            m_image._SmoothGaussian(51);

            Log("Row: " + m_rows + " Cols: " + m_cols + " Size[" + m_sizex + "," +  m_sizey + "]");

            for (int r = 0; r != m_rows; ++r)
            {
                for (int c = 0; c != m_cols; ++c)
                    ProcessCell(r, c);

                Log("R: " + r);
              
            }

            m_image.ROI = new Rectangle(0, 0, m_cols * m_sizex, m_rows * m_sizey);
            m_image.Save("dump.jpg");
        }
    }
}
