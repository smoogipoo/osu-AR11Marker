using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using BMAPI;

namespace osu_AR11Maker
{
    class Program
    {
        //Manual fix to compensate for slight difference
        //In osu! DT tempo increase
        const int UniversalOffset = -95;

        readonly static Process p = new Process();

        [STAThread]
        static void Main(string[] args)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = @"Select a beatmap .osu file";
                while (!ofd.FileName.EndsWith(".osu"))
                {
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        Beatmap b = null;
                        try
                        {
                            b = new Beatmap(ofd.FileName);
                        }
                        catch (Exception e)
                        {
                            MessageBox.Show(string.Format("Beatmap could not be opened: {0} \n {1} \n {2} \n Application will now exit.",
                                                          e.Message, e.InnerException, e.StackTrace));
                            Application.Exit();
                        }

                        string oldAudioFileName = b.AudioFilename;
                        b.Version += " AR11+DT";
                        b.AudioFilename = "33%" + b.AudioFilename;
                        b.ApproachRate = 10;
                        b.SliderMultiplier *= 1.5;

                        string beatmapLocation = b.Filename.Substring(0, b.Filename.LastIndexOf(@"\", StringComparison.InvariantCulture));
                        string targetBeatmapFile = string.Format("{0} - {1} ({2}) [{3}].osu", b.Artist, b.Title, b.Creator, b.Version);
                        bool written = false;

                        p.StartInfo.UseShellExecute = false;
                        p.StartInfo.RedirectStandardOutput = true;
                        p.StartInfo.CreateNoWindow = true;

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine(@"Saving as: " + targetBeatmapFile);
                        Console.ForegroundColor = ConsoleColor.White;

                        Console.WriteLine();
                        Console.WriteLine(@"Cleaning up...");
                        CleanUpLocalDirectory();

                        Console.WriteLine(@"Applying magic to the sound...");
                        while (!written)
                        {
                            try
                            {
                                File.Copy(Path.Combine(beatmapLocation, oldAudioFileName), Path.Combine(Application.StartupPath, "temp.mp3"), true);
                                written = true;
                            }
                            catch
                            {
                                MessageBox.Show("Failed to write to file: " + Path.Combine(Application.StartupPath, "temp.mp3") + "\nPlease ensure the file is not open and click OK to continue.", "AR11Maker", MessageBoxButtons.OK);
                            }
                        }
                        Console.WriteLine(@"1/6");

                        DecodeToWav("temp.mp3", "temp.wav");
                        Console.WriteLine(@"2/6");

                        StretchWav("temp.wav", "temp2.wav");
                        Console.WriteLine(@"3/6");

                        File.Delete(Path.Combine(Application.StartupPath, "temp.mp3"));
                        Console.WriteLine(@"4/6");

                        EncodeToMP3("temp2.wav", "temp3.mp3");
                        Console.WriteLine(@"5/6");

                        written = false;
                        while (!written)
                        {
                            try
                            {
                                File.Copy(Path.Combine(Application.StartupPath, "temp3.mp3"), Path.Combine(beatmapLocation, b.AudioFilename), true);
                                written = true; 
                            }
                            catch
                            {
                                MessageBox.Show("Failed to write to file: " + Path.Combine(Application.StartupPath, "temp.mp3") + "\nPlease ensure the file is not open and click OK to continue.", "AR11Maker", MessageBoxButtons.OK);
                            }
                        }

                        Console.WriteLine(@"Done.");

                        Console.WriteLine();

                        Console.WriteLine(@"Processing events...");
                        foreach (BaseEvent e in b.Events)
                        {
                            //Delay event start time
                            e.StartTime = (int)(e.StartTime * 1.5 + UniversalOffset);
                            if (e.GetType() == typeof(BreakInfo))
                                ((BreakInfo)e).EndTime = (int)(((BreakInfo)e).EndTime * 1.5 + UniversalOffset);
                        }
                        Console.WriteLine(@"Done. Processing timing points...");
                        foreach (TimingPointInfo tP in b.TimingPoints)
                        {
                            //Decrease BPM, delay timing point start time
                            tP.BpmDelay = tP.BpmDelay / 0.6666666;
                            tP.Time = (int)(tP.Time * 1.5 + UniversalOffset);
                        }
                        Console.WriteLine(@"Done. Processing hitobjects...");
                        foreach (BaseCircle obj in b.HitObjects)
                        {
                            //Delay hitobject start time
                            obj.StartTime = (int)(obj.StartTime * 1.5 + UniversalOffset);
                            if (obj.GetType() == typeof(SpinnerInfo))
                                ((SpinnerInfo)obj).EndTime = (int)(((SpinnerInfo)obj).EndTime * 1.5 + UniversalOffset);
                        }
                        Console.WriteLine(@"Done. Saving...");
                        b.Save(Path.Combine(beatmapLocation, targetBeatmapFile));
                        Console.WriteLine(@"Done.");

                        Console.WriteLine();

                        Console.WriteLine(@"Cleaning up...");
                        CleanUpLocalDirectory();

                        Console.WriteLine(@"All done! You might need to refresh your osu! song list by pressing" +
                                          @"F5 at the song select screen.");
                        Console.WriteLine(@"Press any key to exit");
                        Console.ReadKey();
                    }
                    else
                    {
                        Application.ExitThread();
                        Application.Exit();
                    }
                }         
            }
        }

        /// <summary>
        /// Deletes all temporary files in the local directory.
        /// Awaits completion before code continues.
        /// </summary>
        private static void CleanUpLocalDirectory()
        {
            if (File.Exists(Path.Combine(Application.StartupPath, "temp.mp3")))
                File.Delete(Path.Combine(Application.StartupPath, "temp.mp3"));
            if (File.Exists(Path.Combine(Application.StartupPath, "temp.wav")))
                File.Delete(Path.Combine(Application.StartupPath, "temp.wav"));
            if (File.Exists(Path.Combine(Application.StartupPath, "temp2.wav")))
                File.Delete(Path.Combine(Application.StartupPath, "temp2.wav"));
            if (File.Exists(Path.Combine(Application.StartupPath, "temp3.mp3")))
                File.Delete(Path.Combine(Application.StartupPath, "temp3.mp3"));
        }

        private static void DecodeToWav(string srcFile, string dstFile)
        {
            p.StartInfo.FileName = "lame.exe";
            p.StartInfo.Arguments = "--decode " + srcFile + ' ' + dstFile;
            p.Start();
            p.WaitForExit();
        }

        private static void StretchWav(string srcFile, string dstFile)
        {
            p.StartInfo.FileName = "soundstretch.exe";
            p.StartInfo.Arguments = srcFile + ' ' + dstFile + " -temp=-33.333333";
            p.Start();
            p.WaitForExit();
        }

        private static void EncodeToMP3(string srcFile, string dstFile)
        {
            p.StartInfo.FileName = "lame.exe";
            p.StartInfo.Arguments = srcFile + ' ' + dstFile;
            p.Start();
            p.WaitForExit();
        }
    }
}
