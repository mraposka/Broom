using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ClearMyPc
{
    public partial class ScanPage : Form
    {
        public static class Singleton
        {
            public static string extensions { get; set; }
            public static string path { get; set; }
        }
        public ScanPage()
        {
            InitializeComponent();
        }

        string[] extensions;
        List<string> fileList = new List<string>();
        List<string> duplicateFiles = new List<string>();

        private void scanButton_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(Singleton.extensions))
            {
                extensions = Singleton.extensions.Split(',');
                label1.Text = "Scanning!";
                scanButton.Enabled = false;
                delButton.Enabled = false;
                settingsButton.Enabled = false;
                ScanFiles(Singleton.path);
            }
            else
            {
                MessageBox.Show("Select file path to scan on settings page!");
            }

        }

        private async void delButton_Click(object sender, EventArgs e)
        {
            await DeleteDuplicates(duplicateFiles);
        }

        private void ScanFiles(string directoryPath)
        {
            fileList.Clear();
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += Worker_DoWork;
            worker.ProgressChanged += Worker_ProgressChanged;
            worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
            worker.RunWorkerAsync(directoryPath);
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            string directoryPath = e.Argument.ToString();
            HashSet<string> filePaths = new HashSet<string>();
            ScanDirectoryWithKernel32(directoryPath, filePaths, extensions, (BackgroundWorker)sender, e);
            e.Result = filePaths.ToList();
        }

        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            List<string> files = (List<string>)e.UserState;
            fileList.AddRange(files);
            //UpdateItemCountLabel();
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
                MessageBox.Show(new Form { TopMost = true }, "Error scanning directory: " + e.Error.Message);
            else if (e.Cancelled)
                MessageBox.Show(new Form { TopMost = true }, "Scanning canceled.");
            else
            {
                System.Media.SystemSounds.Exclamation.Play();
                MessageBox.Show(new Form { TopMost = true }, "Scanning completed successfully.");
                scanButton.Enabled = true;
                delButton.Enabled = true;
                settingsButton.Enabled = true;
                label1.Text = "Finding Duplicates";
                FindDuplicates();
            }
        }

        private void UpdateItemCountLabel()
        {
            if (InvokeRequired)
                Invoke(new Action(UpdateItemCountLabel));
            else
                label1.Text = "Scanned File: " + fileList.Count.ToString();
        }

        private void ScanDirectoryWithKernel32(string directoryPath, HashSet<string> filePaths, string[] extensions, BackgroundWorker worker, DoWorkEventArgs e)
        {
            WIN32_FIND_DATA findData;
            IntPtr findHandle = FindFirstFile(Path.Combine(directoryPath, "*.*"), out findData);
            if (findHandle != INVALID_HANDLE_VALUE)
            {
                int batchCount = 1000;
                do
                {
                    if (findData.cFileName != "." && findData.cFileName != "..")
                    {
                        string fullPath = Path.Combine(directoryPath, findData.cFileName);
                        if ((findData.dwFileAttributes & FileAttributes.Directory) == FileAttributes.Directory)
                            ScanDirectoryWithKernel32(fullPath, filePaths, extensions, worker, e);
                        else
                        {
                            string extension = Path.GetExtension(findData.cFileName);
                            if (extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                            {
                                if (filePaths.Add(fullPath))
                                {
                                    List<string> filesToAdd = new List<string> { fullPath }; // Create a new list with a single element
                                    worker.ReportProgress(0, filesToAdd);
                                }
                            }
                        }
                    }
                    if (worker.CancellationPending)
                    {
                        e.Cancel = true;
                        break;
                    }
                } while (FindNextFile(findHandle, out findData));
                FindClose(findHandle);
            }
        }
        #region Kernel32.dll Import
        private const int MAX_PATH = 260;
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct WIN32_FIND_DATA
        {
            public FileAttributes dwFileAttributes;
            public FILETIME ftCreationTime;
            public FILETIME ftLastAccessTime;
            public FILETIME ftLastWriteTime;
            public int nFileSizeHigh;
            public int nFileSizeLow;
            public int dwReserved0;
            public int dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
            public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternateFileName;
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr FindFirstFile(string lpFileName, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool FindNextFile(IntPtr hFindFile, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FindClose(IntPtr hFindFile);
        #endregion

        private async void FindDuplicates()
        {
            string saveDuplicatesToDesktopPath = "C:\\Users\\kadir\\Desktop\\ASD.txt";
            duplicateFiles.Clear();
            await Task.Run(() =>
            {
                var fileGroups = fileList
                    .GroupBy(file => new { Name = Path.GetFileName(file), Size = new FileInfo(file).Length })
                    .Where(g => g.Count() > 1);
                foreach (var group in fileGroups)
                {
                    foreach (string file in group)
                    {
                        duplicateFiles.Add(file);
                    }
                }
            });
            label1.Text = "Found " + (duplicateFiles.Count / 2).ToString() + " duplicates";
            await WriteListToFileAsync(duplicateFiles, saveDuplicatesToDesktopPath);
            delButton.Enabled = true;
        }

        private async Task DeleteDuplicates(List<string> duplicateFiles)
        {
            await Task.Run(() =>
            {
                if (DialogResult.Yes == MessageBox.Show((duplicateFiles.Count / 2).ToString() + " file will be deleted. Are you sure?", "ClearMyPc", MessageBoxButtons.YesNo))
                {
                    label1.Text = "Deleting";

                    HashSet<string> originalFiles = new HashSet<string>();
                    foreach (string file in duplicateFiles)
                    {
                        string fileName = Path.GetFileName(file);
                        if (!originalFiles.Contains(fileName))
                            originalFiles.Add(fileName);
                    }
                    List<string> filesToDelete = new List<string>();
                    foreach (string file in duplicateFiles)
                    {
                        string fileName = Path.GetFileName(file);
                        if (originalFiles.Contains(fileName))
                            originalFiles.Remove(fileName);
                        else
                            filesToDelete.Add(file);
                    }
                    foreach (string fileToDelete in filesToDelete)
                    {
                        try
                        {
                            System.IO.File.Delete(fileToDelete);
                        }
                        catch (Exception ex)
                        {
                            // Handle any exceptions that might occur during file deletion
                            MessageBox.Show(new Form { TopMost = true }, "Error deleting file: " + ex.Message);
                        }
                    }
                    Invoke(new Action(() =>
                    {
                        foreach (string file in filesToDelete)
                            duplicateFiles.Remove(file);
                    }));
                    label1.Text = "Deleting Completed";
                }
            });
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            delButton.Enabled = false;  
        }

        private void settingsButton_Click(object sender, EventArgs e)
        {
            SettingsPage settingsPage = new SettingsPage();
            settingsPage.Show();
        }

        private void minimizeButton_Click(object sender, EventArgs e)
        {
            foreach (Form item in Application.OpenForms)
            {
                item.TopMost = false;
            }
            this.WindowState = FormWindowState.Minimized; 
        } 
        private void ScanPage_SizeChanged(object sender, EventArgs e)
        {
            if (this.WindowState != FormWindowState.Minimized)
            {
                this.TopMost = true;
            }
        }
        public async Task WriteListToFileAsync(List<string> lines, string filePath)
        {
            Thread.Sleep(1000);
            label1.Text = "Writing file paths"; 
            checkBox1.BackColor = Color.Yellow;
            const int bufferSize = 8192; // Set the buffer size (e.g., 8KB)

            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.Asynchronous))
            using (var streamWriter = new StreamWriter(fileStream))
            {
                foreach (string line in lines)
                {
                    await streamWriter.WriteLineAsync(line);
                }
            }
            checkBox1.BackColor = Color.Lime;
            label1.Text = "Click delete to delete " + (duplicateFiles.Count / 2).ToString() + " duplicates";
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
           if(checkBox1.Checked) checkBox1.BackColor = Color.Red;
           else checkBox1.BackColor = SystemColors.Control;
        }
    }
}
