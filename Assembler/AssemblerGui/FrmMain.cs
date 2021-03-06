﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Assembler;
using Assembler.Frontend;
using AssemblerGui.Properties;

namespace AssemblerGui
{
    public partial class FrmMain : Form
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        private event SimpleEventHandler OnStateChanged;

        private bool m_Downloading;

        private void Setup()
        {
            SetProcessDPIAware();
            InitializeComponent();
            Icon = Resources.Logo;

            启用长跳转LToolStripMenuItem.Checked = Settings.Default.EnableLongJump;
            启用扩展指令EToolStripMenuItem.Checked = Settings.Default.EnableExtension;
            文件末尾自动停机HToolStripMenuItem.Checked = Settings.Default.AppendHalt;

            OnStateChanged += UpdateTitle;
            OnStateChanged += ToggleEditorMenus;
            OnStateChanged += ToggleAssemblerMenus;
            OnStateChanged += ToggleDebuggerMenus;
            OnStateChanged += () =>
                              {
                                  foreach (var ed in Editors)
                                      ed.ReadOnly = m_Downloading || m_Debugger != null;
                              };
            OnStateChanged += () =>
                              {
                                  foreach (var ed in Editors)
                                      ed.EnableExtension = Settings.Default.EnableExtension;
                              };

            SetupDebugger();
        }

        private void LoadLastFiles()
        {
            if (Settings.Default.Files == null ||
                Settings.Default.Files.Count == 0)
                NewFile();
            else
            {
                TryOpenFiles(Settings.Default.Files.Cast<string>());
                if (!Editors.Any())
                    NewFile();
            }
        }

        public FrmMain()
        {
            Setup();

            LoadLastFiles();

            ActiveControl = TheEditor?.ActiveControl;
        }

        public FrmMain(ICollection<string> args)
        {
            Setup();

            if (args == null ||
                !args.Any())
                LoadLastFiles();
            else
                TryOpenFiles(args);

            ActiveControl = TheEditor?.ActiveControl;
        }

        private void UpdateTitle()
        {
            var sb = new StringBuilder();

            sb.Append(m_Debugger != null ? "MIPS调试器" : "MIPS编辑器");

            sb.Append($" v{Application.ProductVersion}");

            if (Settings.Default.EnableLongJump)
                sb.Append(" [长跳转]");
            if (Settings.Default.EnableExtension)
                sb.Append(" [扩展指令]");

            if (TheEditor != null)
            {
                sb.Append($" [{TheEditor.FileName}]");
                if (TheEditor.Edited)
                    sb.Append("*");
            }

            if (m_Debugger != null && m_IsRunning)
                sb.Append(" - Running");

            if (m_Downloading)
                sb.Append(" - Downloading");

            Text = sb.ToString();
        }

        public static string PromptSaveDialog(string ext, string desc, string title, string fileName, bool isExport)
        {
            var defPath = isExport ? Settings.Default.ExportPath : Settings.Default.SourcePath;
            var dialog =
                new SaveFileDialog
                    {
                        AddExtension = true,
                        AutoUpgradeEnabled = true,
                        CheckFileExists = false,
                        CheckPathExists = true,
                        CreatePrompt = false,
                        DefaultExt = ext,
                        Filter = $"{desc} (*.{ext})|*.{ext}|所有文件 (*)|*",
                        FileName = fileName,
                        Title = title,
                        InitialDirectory = !string.IsNullOrEmpty(defPath)
                                               ? defPath
                                               : AppDomain.CurrentDomain.BaseDirectory
                    };

            var res = dialog.ShowDialog();
            if (res == DialogResult.Cancel)
                return null;

            if (isExport)
                Settings.Default.ExportPath = Path.GetDirectoryName(dialog.FileName);
            else
                Settings.Default.SourcePath = Path.GetDirectoryName(dialog.FileName);
            Settings.Default.Save();

            return dialog.FileName;
        }

        private bool ExportFile<T>(T asm, Func<string> prompt, bool open = true)
            where T : AsmProgBase, IWriter
        {
            asm.EnableLongJump = Settings.Default.EnableLongJump;
            try
            {
                var pre = SaveDependency();
                if (pre == null)
                    return false;

                string fn;
                using (var mem = new MemoryStream())
                using (var sw = new StreamWriter(mem))
                {
                    asm.SetWriter(sw);
                    SetFrontend(asm);
                    try
                    {
                        foreach (var p in pre)
                            asm.Feed(p, Settings.Default.AppendHalt);
                        asm.Done();
                    }
                    catch (AssemblyException e)
                    {
                        MessageBox.Show(e.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        TryOpenFile(e.FilePath, e.Line, e.CharPos);
                        return false;
                    }
                    sw.Flush();

                    mem.Position = 0;
                    fn = prompt();
                    if (fn == null)
                        return false;

                    using (var ou = File.Open(fn, FileMode.Create, FileAccess.Write))
                        mem.CopyTo(ou);
                }
                if (!open)
                    return true;

                var msg = MessageBox.Show(
                                          "导出成功，是否要用记事本打开？",
                                          "MIPS汇编器",
                                          MessageBoxButtons.YesNo,
                                          MessageBoxIcon.Question,
                                          MessageBoxDefaultButton.Button2);
                if (msg != DialogResult.Yes)
                    return true;

                Process.Start("notepad", fn);
                return true;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private static void SetFrontend(AsmProgBase asm)
        {
            if (Settings.Default.EnableExtension)
                asm.Frontend = new AntlrExtendedFrontend();
            else
                asm.Frontend = new AntlrStandardFrontend();
        }

        private Preprocessor SaveDependency()
        {
            if (TheEditor.FilePath == null)
                if (!TheEditor.PerformSave())
                    return null;

            return SaveDependency(TheEditor.FilePath);
        }

        private Preprocessor SaveDependency(string initial)
        {
            var pre = new Preprocessor { initial };
            var flag = true;
            while (flag)
            {
                flag = false;
                var lst = new List<string>();
                foreach (var fn in pre)
                {
                    var e = Editors.FirstOrDefault(ed => ed.FilePath == fn);
                    if (e == null ||
                        !e.Edited)
                        continue;
                    e.PerformSave();
                    lst.Add(fn);
                    flag = true;
                }
                pre.AddRange(lst);
            }
            return pre;
        }

        private void Cycle<T>(T asm)
            where T : AsmProgBase, IWriter
        {
            asm.EnableLongJump = Settings.Default.EnableLongJump;
            var tmp = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tmp, TheEditor.Value);
                using (var mem = new MemoryStream())
                using (var sw = new StreamWriter(mem))
                {
                    asm.SetWriter(sw);
                    SetFrontend(asm);
                    try
                    {
                        asm.Feed(tmp, false);
                        asm.Done();
                    }
                    catch (AssemblyException e)
                    {
                        MessageBox.Show(
                                        e.Message.Replace(tmp, TheEditor.FilePath),
                                        "错误",
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Error);
                        TryOpenFile(TheEditor.FilePath, e.Line, e.CharPos);
                        return;
                    }
                    sw.Flush();

                    mem.Position = 0;

                    using (var sr = new StreamReader(mem))
                        TheEditor.Value = sr.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                File.Delete(tmp);
            }
        }

        private void ToggleAssemblerMenus()
        {
            if (m_Downloading || m_Debugger != null)
            {
                intelHex文件ToolStripMenuItem.Enabled = false;
                二进制机器码BToolStripMenuItem.Enabled = false;
                十六进制机器码HToolStripMenuItem.Enabled = false;
                原始汇编AToolStripMenuItem.Enabled = false;

                下载DToolStripMenuItem.Enabled = false;

                格式化代码FToolStripMenuItem.Enabled = false;
                启用长跳转LToolStripMenuItem.Enabled = false;
                启用扩展指令EToolStripMenuItem.Enabled = false;
            }
            else
            {
                intelHex文件ToolStripMenuItem.Enabled = TheEditor != null;
                二进制机器码BToolStripMenuItem.Enabled = TheEditor != null;
                十六进制机器码HToolStripMenuItem.Enabled = TheEditor != null;
                原始汇编AToolStripMenuItem.Enabled = TheEditor != null;

                下载DToolStripMenuItem.Enabled = TheEditor != null;

                格式化代码FToolStripMenuItem.Enabled = TheEditor != null;
                启用长跳转LToolStripMenuItem.Enabled = true;
                启用扩展指令EToolStripMenuItem.Enabled = true;
            }
        }

        private void FrmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!SaveAll())
                e.Cancel = true;
            else
            {
                if (Settings.Default.Files == null)
                    Settings.Default.Files = new StringCollection();
                Settings.Default.Files.Clear();
                foreach (var ed in Editors)
                    if (ed.FilePath != null)
                        Settings.Default.Files.Add(ed.FilePath);
                Settings.Default.Save();
            }
        }

        private static bool PromptAdvance()
        {
            if (Settings.Default.Advance)
                return true;

            var res =
                MessageBox.Show(
                                @"警告：启用长跳转 和/或 启用扩展指令 可能会造成意想不到的后果，" +
                                @"请确保已经参阅软件源代码、了解长跳转机制和扩展指令所有细节！" +
                                @"是否确认启用？",
                                "高级功能",
                                MessageBoxButtons.OKCancel,
                                MessageBoxIcon.Exclamation,
                                MessageBoxDefaultButton.Button2);
            if (res == DialogResult.Cancel)
                return false;

            Settings.Default.Advance = true;
            Settings.Default.Save();
            return true;
        }

        private void intelHex文件ToolStripMenuItem_Click(object sender, EventArgs e) =>
            ExportFile(
                       new IntelAssembler(),
                       () => PromptSaveDialog("hex", "Intel Hex文件", "导出", TheEditor.FileName, true));

        private void 二进制机器码BToolStripMenuItem_Click(object sender, EventArgs e) =>
            ExportFile(
                       new BinAssembler(),
                       () => PromptSaveDialog("txt", "纯文本文件", "导出", TheEditor.FileName, true));

        private void 十六进制机器码HToolStripMenuItem_Click(object sender, EventArgs e) =>
            ExportFile(
                       new HexAssembler(),
                       () => PromptSaveDialog("txt", "纯文本文件", "导出", TheEditor.FileName, true));

        private void 原始汇编AToolStripMenuItem_Click(object sender, EventArgs e) =>
            ExportFile(
                       new AsmFinalPrettifier(),
                       () => PromptSaveDialog("mips", "MIPS文件", "导出", TheEditor.FileName, true));

        private void 格式化代码FToolStripMenuItem_Click(object sender, EventArgs e) => Cycle(new AsmPrettifier());

        private void 查看帮助VToolStripMenuItem_Click(object sender, EventArgs e) => FrmHelp.ShowHelp(this);

        private void 下载DToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!Downloader.CheckStpExistance(this))
                return;

            var path = Path.GetTempFileName();
            File.Move(path, path + ".hex");
            var hexPath = path + ".hex";

            if (!ExportFile(new IntelAssembler(), () => hexPath, false))
                return;

            m_Downloading = true;
            OnStateChanged?.Invoke();

            var downloader = new Downloader(hexPath);
            downloader.OnExited +=
                msg =>
                {
                    if (msg == null)
                        MessageBox.Show("下载成功！", "MIPS汇编器", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    else
                        MessageBox.Show("下载失败，错误信息：" + msg, "MIPS汇编器", MessageBoxButtons.OK, MessageBoxIcon.Error);

                    File.Delete(hexPath);
                    m_Downloading = false;
                    InvokeOnMainThread(OnStateChanged)();
                };
            downloader.Start();
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e) => OnStateChanged?.Invoke();

        private void 全部关闭WToolStripMenuItem_Click(object sender, EventArgs e)
        {
            while (TheEditor != null)
                if (!TheEditor.SaveClose())
                    break;
        }

        private void FrmMain_DragDrop(object sender, DragEventArgs e)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null)
                return;
            TryOpenFiles(files);
        }

        private void FrmMain_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void 启用长跳转LToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!Settings.Default.EnableLongJump)
                if (!PromptAdvance())
                    return;

            Settings.Default.EnableLongJump ^= true;
            Settings.Default.Save();
            启用长跳转LToolStripMenuItem.Checked = Settings.Default.EnableLongJump;
            OnStateChanged?.Invoke();
        }

        private void 启用扩展指令EToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!Settings.Default.EnableLongJump)
                if (!PromptAdvance())
                    return;

            Settings.Default.EnableExtension ^= true;
            Settings.Default.Save();
            启用扩展指令EToolStripMenuItem.Checked = Settings.Default.EnableExtension;
            OnStateChanged?.Invoke();
        }

        private void 文件末尾自动停机HToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.AppendHalt = 文件末尾自动停机HToolStripMenuItem.Checked;
            Settings.Default.Save();
        }

        private void 联系作者AToolStripMenuItem_Click(object sender, EventArgs e) =>
            Process.Start(
                          $"mailto:b1f6c1c4@gmail.com?subject=MIPS%20Assembler&body=Version%20{WebUtility.UrlEncode(Application.ProductVersion)}");
    }
}
