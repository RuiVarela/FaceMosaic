using Cognitec.FRsdk;
using Emgu.CV;
using Emgu.CV.Structure;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;

namespace FacePrivacy
{
    class Cognitec87FaceDetector : FaceDetector
    {
        public float MininumRelativeEyeDistance { get; set; }
        public float MaximumRelativeEyeDistance { get; set; }
        public bool Initialized { get; set; }

        private Cognitec.FRsdk.Face.Finder m_facefinder = null;

        public Cognitec87FaceDetector(Bgr debug)
        {
            DebugColor = debug;
            MininumRelativeEyeDistance = 0.001f;
            MaximumRelativeEyeDistance = 0.6f;
            Initialized = false;

            Configuration cfg = null;
            try
            {
                cfg = new Configuration("frsdk-8.7.0.cfg");

                Configuration.ProtectedItem[] pitems = cfg.protectedItems();
                Log("Protected Items: ");
                foreach (Configuration.ProtectedItem pitem in pitems)
                {
                    Log(pitem.key + " : " + pitem.value);
                }
            }
            catch (System.Exception ex)
            {
                Log("Exception \n"+ ex.Message);
                return;
            }

            m_facefinder = new Cognitec.FRsdk.Face.Finder(cfg);

            Initialized = true;
        }

        public override Result Detect(Image<Bgr, Byte> image)
        {
            List<Rectangle> faces = new List<Rectangle>();

            try
            {
                Cognitec.FRsdk.Image img = null;


                Bitmap bt = image.ToBitmap();
                
                byte[] bitmap_info = GetBitmapInfo(bt.PixelFormat, bt.Width, bt.Height, null);

                unsafe
                {
                    fixed (byte* p = bitmap_info)
                    {
                        BitmapData bitmapdata = bt.LockBits(new Rectangle(0, 0, bt.Width, bt.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                        IntPtr ptr_bitmap_info = (IntPtr)p;
                        img = Bmp.load(ptr_bitmap_info, bitmapdata.Scan0, "face_find");
                        bt.UnlockBits(bitmapdata);
                    }
                }



                int x1 = 0;
                int y1 = 0;
                int x2 = (int)img.width();
                int y2 = (int)img.height();

                Cognitec.FRsdk.Face.Location[] locations = m_facefinder.find(img,
                                                                             MininumRelativeEyeDistance, MaximumRelativeEyeDistance,
                                                                             x1, y1, x2, y2);

                float widthHeightRatio = 1.4f;
                foreach (var face in locations)
                {
                    int width = (int)(face.width * 2.0);
                    int height = (int)(width * widthHeightRatio);

                    int x = (int)(face.pos.x - (width / 2.0));
                    int y = (int)(face.pos.y - (height / 2.0));

                    if (x < 0) x = 0;
                    if (y < 0) y = 0;

                    if (x > img.width()) x = (int)img.width();
                    if (y > img.height()) y = (int)img.height();

                    faces.Add(new Rectangle(x, y, width, height));
                }
            }
            catch (System.Exception ex)
            {
                Log("Exception\n" + ex.Message);
            }

            Result result = new Result();
            result.Faces = faces;
            result.DebugColor = DebugColor;

            return result;
        }



        enum Win32 { BI_RGB = 0, BI_BITFIELDS = 3 }

        // Create a the BITMAPINFO header for a bitmap.
        private static byte[] GetBitmapInfo(PixelFormat format, int width, int height, int[] palette)
        {
            // Set the size of the structure
            int size = 40;
            if (format == PixelFormat.Format16bppRgb565)
                size += 3 * 4;
            else if (format == PixelFormat.Format8bppIndexed)
                size += 256 * 4;
            else if (format == PixelFormat.Format4bppIndexed)
                size += 40 + 16 * 4;
            else if (format == PixelFormat.Format1bppIndexed)
                size += 40 + 2 * 4;
            byte[] bitmapInfo = new byte[size];
            WriteInt32(bitmapInfo, 0, 40); //biSize
            WriteInt32(bitmapInfo, 4, width); //biWidth
            WriteInt32(bitmapInfo, 8, -height); //biHeight
            WriteInt32(bitmapInfo, 12, 1); //biPlanes
            WriteInt32(bitmapInfo, 14, FormatToBitCount(format)); //biBitCount
            if (format == PixelFormat.Format16bppRgb565)
            {
                WriteInt32(bitmapInfo, 16, (int)Win32.BI_BITFIELDS);
                // Setup the masks for 565
                WriteInt32(bitmapInfo, 40, 0xF800); // R Mask
                WriteInt32(bitmapInfo, 44, 0x07E0); // G Mask
                WriteInt32(bitmapInfo, 48, 0x001F); // B Mask
            }
            else
                WriteInt32(bitmapInfo, 16, (int)Win32.BI_RGB); // biCompression

            WriteInt32(bitmapInfo, 20, 0); // biSizeImage
            WriteInt32(bitmapInfo, 24, 3780); // biXPelsPerMeter
            WriteInt32(bitmapInfo, 28, 3780); // biYPelsPerMeter
            WriteInt32(bitmapInfo, 32, 0); // biClrUsed
            WriteInt32(bitmapInfo, 36, 0); // biClrImportant
            //Setup palette
            if (palette != null)
            {
                // Write in RGBQUADS
                for (int i = 0; i < palette.Length; i++)
                    WriteBGR(bitmapInfo, 40 + i * 4, palette[i]);
            }
            return bitmapInfo;
        }

        // Convert a pixel format into a bit count value.
        private static short FormatToBitCount(PixelFormat pixelFormat)
        {
            switch (pixelFormat)
            {
                case PixelFormat.Format1bppIndexed:
                    return 1;

                case PixelFormat.Format4bppIndexed:
                    return 4;

                case PixelFormat.Format8bppIndexed:
                    return 8;

                case PixelFormat.Format16bppRgb555:
                case PixelFormat.Format16bppRgb565:
                case PixelFormat.Format16bppArgb1555:
                case PixelFormat.Format16bppGrayScale:
                    return 16;

                case PixelFormat.Format24bppRgb:
                    return 24;

                case PixelFormat.Format32bppRgb:
                case PixelFormat.Format32bppPArgb:
                case PixelFormat.Format32bppArgb:
                    return 32;

                case PixelFormat.Format48bppRgb:
                    return 48;

                case PixelFormat.Format64bppPArgb:
                case PixelFormat.Format64bppArgb:
                    return 64;

                default:
                    return 32;
            }
        }

        // Write a BGR value to a buffer as an RGBQUAD.
        private static void WriteBGR(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)0;
        }

        // Write a little-endian 16-bit integer value to a buffer.
        private static void WriteUInt16(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
        }

        // Write a little-endian 32-bit integer value to a buffer.
        private static void WriteInt32(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }
    }
}

