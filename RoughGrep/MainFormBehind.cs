﻿using ScintillaNET;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace RoughGrep
{
    public static class SciUtil
    {
        public static Scintilla CreateScintilla()
        {
            var scintilla = new Scintilla();
            scintilla.Lexer = Lexer.Cpp;

            // Configuring the default style with properties
            // we have common to every lexer style saves time.

            scintilla.StyleResetDefault();
            scintilla.Styles[Style.Default].Font = "Consolas";
            scintilla.Styles[Style.Default].Size = 9;
            scintilla.StyleClearAll();

            // Configure the CPP (C#) lexer styles
            scintilla.Styles[Style.Cpp.Default].ForeColor = Color.Silver;
            scintilla.Styles[Style.Cpp.Comment].ForeColor = Color.FromArgb(0, 128, 0); // Green
            scintilla.Styles[Style.Cpp.CommentLine].ForeColor = Color.FromArgb(0, 128, 0); // Green
            scintilla.Styles[Style.Cpp.CommentLineDoc].ForeColor = Color.FromArgb(128, 128, 128); // Gray
            scintilla.Styles[Style.Cpp.Number].ForeColor = Color.Olive;
            scintilla.Styles[Style.Cpp.Word].ForeColor = Color.Blue;
            scintilla.Styles[Style.Cpp.Word2].ForeColor = Color.Blue;
            scintilla.Styles[Style.Cpp.String].ForeColor = Color.FromArgb(163, 21, 21); // Red
            scintilla.Styles[Style.Cpp.Character].ForeColor = Color.FromArgb(163, 21, 21); // Red
            scintilla.Styles[Style.Cpp.Verbatim].ForeColor = Color.FromArgb(163, 21, 21); // Red
            scintilla.Styles[Style.Cpp.StringEol].BackColor = Color.Pink;
            scintilla.Styles[Style.Cpp.Operator].ForeColor = Color.Purple;
            scintilla.Styles[Style.Cpp.Preprocessor].ForeColor = Color.Maroon;
            scintilla.Lexer = Lexer.Cpp;

            // Set the keywords
            scintilla.SetKeywords(0, "abstract as base break case catch checked continue default delegate do else event explicit extern false finally fixed for foreach goto if implicit in interface internal is lock namespace new null object operator out override params private protected public readonly ref return sealed sizeof stackalloc switch this throw true try typeof unchecked unsafe using virtual while");
            scintilla.SetKeywords(1, "bool byte char class const decimal double enum float int long sbyte short static string struct uint ulong ushort void");

            scintilla.ScrollWidth = 0;
            scintilla.ScrollWidthTracking = true;

            return scintilla;
        }
        public static void SearchAndMove(Scintilla scintilla, string searchText, bool reverse = false)
        {
            if (reverse)
            {
                scintilla.TargetStart = scintilla.CurrentPosition - 1;
                scintilla.TargetEnd = 0;
            }
            else
            {
                scintilla.TargetStart = scintilla.CurrentPosition;
                scintilla.TargetEnd = scintilla.TextLength;

            }
            int pos = scintilla.SearchInTarget(searchText);
            if (pos != -1)
            {
                scintilla.SelectionStart = scintilla.TargetStart;
                scintilla.SelectionEnd = scintilla.TargetEnd;
                scintilla.ScrollCaret();
            }
        }
        public static void TouchAfterTextLoad(Scintilla scintilla)
        {
            scintilla.ScrollWidth = 1;
            scintilla.ScrollWidthTracking = true;
        }

        public static void SetAllText(Scintilla scintilla, string text)
        {
            scintilla.Text = text;
            TouchAfterTextLoad(scintilla);
        }

        public static void RevealLine(Scintilla scintilla, int line)
        {
            var linesOnScreen = scintilla.LinesOnScreen - 2; // Fudge factor

            var start = scintilla.Lines[line - (linesOnScreen / 2)].Position;
            var end = scintilla.Lines[line + (linesOnScreen / 2)].Position;
            scintilla.ScrollRange(start, end);
        }
    }

    public class MainFormBehind
    {
        private readonly MainFormUi Ui;
       
        public MainFormBehind(MainFormUi ui)
        {
            void SetupScintilla()
            {
                var scintilla = SciUtil.CreateScintilla();
                scintilla.UpdateUI += (o, e) =>
                {
                    if (e.Change == UpdateChange.Selection)
                    {
                        UpdateStatusBar();
                    }
                };
                //sci.ReadOnly = true;
                ui.resultBox = scintilla;
                ui.form.Controls.Add(ui.resultBox);
                ui.resultBox.Dock = DockStyle.Fill;
                ui.searchTextBox.Select();
                ui.tableLayout.Controls.Add(ui.resultBox, 0, 1);
            }

            this.Ui = ui;
            SetupScintilla();
            ui.searchTextBox.KeyDown += (o, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    SearchStartEvent(ui, e);
                }
            };
            ui.dirSelector.KeyDown += (o, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    SearchStartEvent(ui, e);
                }
            };

            ui.resultBox.KeyDown += (o, e) =>
            {
                HandleKeyDownOnResults(e, ui.resultBox.CurrentLine);
            };
            ui.resultBox.KeyPress += (o, e) =>
            {
                // prevent PLING sound
                e.Handled = true;
            };
            SciUtil.SetAllText(ui.resultBox, "Tutorial: space=preview, enter=edit, p=edit parent project dir");

            ui.dirSelector.DataSource = Logic.DirHistory;
            ui.searchTextBox.DataSource = Logic.SearchHistory;
            ui.rgArgsComboBox.Items.AddRange(new[]
            {
                Logic.RgExtraArgs,
                "--files",
                "-m 5 --smart-case", 
                "-M 1000",
                "-g *.cs -g *.csproj",
                "--no-ignore",
                "--context 2"

            });
            ui.rgArgsComboBox.TextChanged += (o, e) =>
            {
                Logic.RgExtraArgs = ui.rgArgsComboBox.Text;
                UpdateStatusBar();
            };

            ui.dirSelector.Text = Logic.WorkDir;
            ui.btnAbort.Click += (o, e) =>
            {
                ui.btnAbort.Visible = false;
                Logic.KillSearch();
            };
            LiveSearchEvents(ui);
            UpdateStatusBar();
        }
        private readonly Lazy<FullPreviewForm> Previewer = new Lazy<FullPreviewForm>(() =>
        {
            return new FullPreviewForm();
        });

        private void UpdateStatusBar()
        {
            var (file, line) = Logic.LookupFileAtLine(Ui.resultBox.CurrentLine, relative: true);
            Ui.statusLabel.Text = $"{file} +{line}";
            Ui.statusLabelCurrentArgs.Text = Logic.RgExtraArgs;
            

            
        }
        private void LiveSearchEvents(MainFormUi ui)
        {
            var ctrl = ui.searchControl;
            var rb = ui.resultBox;
            ctrl.searchTextBox.TextChanged += (o, e) => SciUtil.SearchAndMove(rb, ctrl.searchTextBox.Text);
            ctrl.btnNext.Click += (o, e) => SciUtil.SearchAndMove(rb, ctrl.searchTextBox.Text);
            ctrl.btnPrev.Click += (o, e) => SciUtil.SearchAndMove(rb, ctrl.searchTextBox.Text, reverse: true);
            ctrl.searchTextBox.KeyDown += (o, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    if (e.Shift)
                    {
                        SciUtil.SearchAndMove(rb, ctrl.searchTextBox.Text, reverse: true);
                    } else
                    {
                        SciUtil.SearchAndMove(rb, ctrl.searchTextBox.Text);
                    }
                    e.SuppressKeyPress = true;
                    e.Handled = true;
                }
            };

        }

        private static void SearchStartEvent(MainFormUi ui, KeyEventArgs e)
        {
            e.SuppressKeyPress = true;
            e.Handled = true;
            Logic.StartSearch(ui);
        }

        internal void HandleKeyDownOnResults(KeyEventArgs e, int line)
        {
            var supress = true;
            switch (e.KeyCode)
            {
                case Keys.Space:
                    {
                        var (file, lineNum) = Logic.LookupFileAtLine(line);
                        if (file != null)
                        {
                            PreviewFile(file, lineNum);
                        }
                        break;
                    }
                case Keys.Enter:
                    {
                        var (file, lineNum) = Logic.LookupFileAtLine(line);
                        if (file != null)
                        {
                            EditFile(file, lineNum);
                        }
                        break;
                    }
                case Keys.P:
                    {
                        var (file, lineNum) = Logic.LookupFileAtLine(line);
                        if (file != null)
                        {
                            OpenProject(file, lineNum);
                        }
                        break;
                    }

                default:
                    {
                        supress = false;
                    }
                    break;
            }
            if (supress)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
            }

        }

        private static string ScanParentsForFiles(string startPath, string[] globs)
        {
            var cur = startPath;
            while (true)
            {
                var parent = Directory.GetParent(cur);
                if (parent == null)
                {
                    return null;
                }
                cur = parent.FullName;
                foreach (var trie in globs)
                {
                    var files = Directory.GetFiles(cur, trie);
                    if (files.Length > 0)
                    {
                        return cur;
                    }
                }
            }
        }
        private void OpenProject(string file, int lineNum)
        {
            var tries = new[] { "package.json", "*.csproj", "*.fsproj", ".gitignore", "*.sln" };
            var dir = ScanParentsForFiles(file, tries);
            if (dir != null)
            {
                LaunchEditorWithArgs($"{dir} -g {file}:{lineNum}");
            }
        }

        void PreviewFile(string path, int linenum)
        {
            var text = File.ReadAllText(path);
            var fp = Previewer.Value;
            SciUtil.SetAllText(fp.scintilla, text);
            fp.Text = path;

            var pos = fp.scintilla.Lines[linenum-1].Position;
            SciUtil.RevealLine(fp.scintilla, linenum - 1);

            fp.scintilla.GotoPosition(pos);
            SciUtil.SearchAndMove(fp.scintilla, Ui.searchTextBox.Text);
              
            fp.Show();
        }

        void LaunchEditorWithArgs(string args)
        {
            var psi = Logic.CreateStartInfo("code", args);
            psi.UseShellExecute = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            Process.Start(psi);
        }
        void EditFile(string file, int lineNum)
        {
            LaunchEditorWithArgs($"-g {file}:{lineNum}");
        }
    }
}
