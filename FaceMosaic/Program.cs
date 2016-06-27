using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.UI;
using System.Threading;

namespace FaceMosaic
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string source = ".\\DatabaseSource\\";
            string database = ".\\Database\\";
            string image = ".\\DSCN0191.JPG";

            //FaceDatabaseBuilder builder = new FaceDatabaseBuilder();
            //builder.Work(source, database);

            DatabaseMatcher matcher = new DatabaseMatcher();
            matcher.Work(database, image);
        }
    }
}