using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Net.WebRequestMethods;
using static System.Windows.Forms.AxHost;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace ClearMyPc
{

    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        string[] extensions = { ".jpg", ".png", ".txt" };
        List<string> duplicateFiles;
        private void backgroundWorker2_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            label1.Text = "Taranan Dosya Sayısı:" + listBox1.Items.Count.ToString();
        }
        private void button1_Click(object sender, EventArgs e)
        {
            label1.Text = "Scanning!";
            //ScanFiles("C:\\Users\\kadir\\Desktop\\A");
            ScanFiles("D:\\X");
        }
        private async void button2_Click(object sender, EventArgs e)
        {
            await DeleteDuplicates(duplicateFiles);
        }
        private void ScanFiles(string directoryPath)
        {
            listBox1.Items.Clear();
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
            listBox1.Items.AddRange(files.ToArray());
            UpdateItemCountLabel();
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
                MessageBox.Show("Error scanning directory: " + e.Error.Message);
            else if (e.Cancelled)
                MessageBox.Show("Scanning canceled.");
            else
            { MessageBox.Show("Scanning completed successfully."); FindDuplicates();}
        }

        private void UpdateItemCountLabel()
        {
            if (InvokeRequired)
                Invoke(new Action(UpdateItemCountLabel));
            else
                label1.Text = "Scanned File: " + listBox1.Items.Count.ToString();
        }

        private void ScanDirectoryWithKernel32(string directoryPath, HashSet<string> filePaths, string[] extensions, BackgroundWorker worker, DoWorkEventArgs e)
        {
            WIN32_FIND_DATA findData;
            IntPtr findHandle = FindFirstFile(Path.Combine(directoryPath, "*.*"), out findData);
            if (findHandle != INVALID_HANDLE_VALUE)
            {
                List<string> filesToAdd = new List<string>();
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
                                    filesToAdd.Add(fullPath);
                                }
                            }
                        }
                    }
                    if (filesToAdd.Count >= batchCount)
                    {
                        worker.ReportProgress(0, filesToAdd);
                        filesToAdd.Clear();
                    }
                    if (worker.CancellationPending)
                    {
                        e.Cancel = true;
                        break;
                    }
                }
                while (FindNextFile(findHandle, out findData));
                FindClose(findHandle);
                if (filesToAdd.Count > 0)
                    worker.ReportProgress(0, filesToAdd);
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
            listBox2.Items.Clear();
            List<string> _duplicateFiles = new List<string>();
            await Task.Run(() =>
            {
                var fileGroups = listBox1.Items.Cast<string>()
                    .GroupBy(file => new { Name = Path.GetFileName(file), Size = new FileInfo(file).Length })
                    .Where(g => g.Count() > 1);
                foreach (var group in fileGroups)
                {
                    foreach (string file in group)
                    {
                        _duplicateFiles.Add(file);
                        Invoke(new Action(() => listBox2.Items.Add(file)));
                    }
                }
            });
            label1.Text = "Found " + (listBox2.Items.Count/2).ToString() + " duplicates";
            duplicateFiles = _duplicateFiles;
        }

        private async Task DeleteDuplicates(List<string> duplicateFiles)
        {
            await Task.Run(() =>
            {
                if (DialogResult.Yes == MessageBox.Show((listBox2.Items.Count/2).ToString() + " file will be deleted. Are you sure?", "ClearMyPc", MessageBoxButtons.YesNo))
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
                            MessageBox.Show("Error deleting file: " + ex.Message);
                        }
                    }

                    Invoke(new Action(() =>
                    {
                        foreach (string file in filesToDelete)
                            listBox2.Items.Remove(file);
                    }));
                }
            });
        } 
    }
}
