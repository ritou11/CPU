﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace AssemblerGui
{
    public partial class FrmMain
    {
        private bool m_IsInitial = true;

        private Editor TheEditor => tabControl1.ActiveDocument as Editor;

        private IEnumerable<Editor> Editors => tabControl1.Documents.Cast<Editor>();

        private static string PromptOpen()
        {
            var dialog =
                new OpenFileDialog
                    {
                        AddExtension = true,
                        AutoUpgradeEnabled = true,
                        CheckFileExists = true,
                        CheckPathExists = true,
                        DefaultExt = "mips",
                        Filter = "MIPS文件 (*.mips)|*.mips|所有文件 (*)|*",
                        Title = "打开"
                    };
            var res = dialog.ShowDialog();
            if (res == DialogResult.Cancel)
                return null;

            return dialog.FileName;
        }

        private Editor MakeNewEditor()
        {
            var the = new Editor();
            the.OnStateChanged += () => OnStateChanged?.Invoke();
            the.OnToggleBreakPoint += ToggledBreakPoint;
            the.Show(tabControl1, DockState.Document);
            return the;
        }

        private void NewFile()
        {
            var the = MakeNewEditor();
            the.Focus();
            OnStateChanged?.Invoke();
        }

        private void OpenFile(string str, int? line = null, int? charPos = null, bool debugging = false, bool force = false)
        {
            Editor the;
            if (m_IsInitial &&
                tabControl1.DocumentsCount == 1 &&
                !TheEditor.Edited)
                the = TheEditor;
            else
                the = Editors.FirstOrDefault(ed => ed.FilePath == str);

            m_IsInitial = false;

            var isNew = the == null;
            the = the ?? MakeNewEditor();
            the.Focus();
            the.LoadDoc(str, line, charPos, debugging, force);
            if (isNew)
                foreach (var s in m_BreakPoints.Where(s => s.FilePath == str))
                    the.ToggleBreakPoint(s.Line);
            OnStateChanged?.Invoke();
        }

        private bool SaveAll(bool forbidNo = false) =>
            Editors.All(ed => ed.PromptForSave(forbidNo));

        private void ToggleEditorMenus()
        {
            if (m_Debugger == null)
            {
                新建NToolStripMenuItem.Enabled = true;
                打开OToolStripMenuItem.Enabled = true;

                保存SToolStripMenuItem.Enabled = TheEditor != null;
                全部保存LToolStripMenuItem.Enabled = TheEditor != null;
                另存为AToolStripMenuItem.Enabled = TheEditor != null;

                关闭CToolStripMenuItem.Enabled = TheEditor != null;
            }
            else
            {
                新建NToolStripMenuItem.Enabled = false;
                打开OToolStripMenuItem.Enabled = true;

                保存SToolStripMenuItem.Enabled = false;
                全部保存LToolStripMenuItem.Enabled = false;
                另存为AToolStripMenuItem.Enabled = false;

                关闭CToolStripMenuItem.Enabled = TheEditor != null;
            }
        }

        private void 新建NToolStripMenuItem_Click(object sender, EventArgs e) => NewFile();

        private void 保存SToolStripMenuItem_Click(object sender, EventArgs e) => TheEditor.PerformSave();

        private void 全部保存LToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (var ed in Editors)
                if (!ed.PerformSave())
                    break;
        }

        private void 另存为AToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!TheEditor.PromptSaveAs())
                return;

            TheEditor.PerformSave();
        }

        private void 打开OToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var str = PromptOpen();
            if (str == null)
                return;

            OpenFile(str);
        }

        private void 退出QToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!SaveAll())
                return;

            Environment.Exit(0);
        }

        private void 切换断点BToolStripMenuItem_Click(object sender, EventArgs e) =>
            TheEditor.ToggleBreakPoint();

        private void 关闭CToolStripMenuItem_Click(object sender, EventArgs e)
        {
            m_IsInitial = false;
            if (!TheEditor.PromptForSave())
                return;

            TheEditor.Close();
            OnStateChanged?.Invoke();
        }
    }
}
