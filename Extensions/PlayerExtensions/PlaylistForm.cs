// This file is a part of MPDN Extensions.
// https://github.com/zachsaw/MPDN_Extensions
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public
// License along with this library.
// 
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using MediaInfoDotNet;
using Mpdn.Extensions.Framework;
using Ookii.Dialogs;

namespace Mpdn.Extensions.PlayerExtensions.Playlist
{
    public partial class PlaylistForm : FormEx
    {
        #region EventHandlers

        public event EventHandler PlaylistChanged;

        #endregion

        #region Playlist Events

        public void PlaylistForm_OnRegexChange(object sender, RegexEventArgs e)
        {
            RegexList = e.RegexList;
            StripDirectoryInFileName = e.StripDirectoryInFileName;
            m_PlayListUi.SyncSettings();

            PopulatePlaylist();
        }

        #endregion

        #region Eventhandler Methods

        private void NotifyPlaylistChanged()
        {
            PlaylistChanged.Handle(h => h(this, EventArgs.Empty));
        }

        public delegate void RegexHandler(object sender, RegexEventArgs e);

        public static event RegexHandler OnRegexChange = delegate { };

        #endregion

        #region Fields

        private Playlist m_PlayListUi;

        private const double MAX_OPACITY = 1.0;
        private const double MIN_OPACITY = 0.8;
        private const string ACTIVE_INDICATOR = "[*]";
        private const string INACTIVE_INDICATOR = "[ ]";

        public const string PLAYLIST_ICONS_DIR = @"Extensions\PlayerExtensions\Images\Playlist";

        private Color m_FontColor = Color.Empty;
        private Color m_FontDropShadowColor = Color.Empty;
        private Color m_SelectionFontColor = Color.Empty;
        private Color m_PlayFontColor = Color.Empty;
        private Color m_ColumnHeaderFontColor = Color.Empty;
        private Color m_FormColor = Color.Empty;
        private Color m_SelectionColor = Color.Empty;
        private Color m_GreyOutColor = Color.Empty;
        private Color m_PlayColor = Color.Empty;
        private Color m_ColumnHeaderColor = Color.Empty;
        private Color m_ColumnHeaderBorderColor = Color.Empty;
        private Color m_StatusBorderColor = Color.Empty;
        private bool m_ColumnHeaderTransparency;
        private bool m_DropShadow;

        private string m_LoadedPlaylist;

        private bool m_FirstShow = true;
        private bool m_WasShowing;

        private bool m_ColumnsFixed;

        private int m_CurrentPlayIndex = -1;
        private int m_SelectedRowIndex = -1;
        private long m_PreviousChapterPosition;

        private bool m_IsDragging;
        private Rectangle m_DragRowRect;
        private int m_DragRowIndex;

        private int m_TitleCellIndex = 4;
        private int m_SkipCellIndex = 5;
        private int m_EndCellIndex = 6;

        private int m_MinWorker;
        private int m_MinIoc;

        private ToolTip m_PlayCountToolTip;

        private AfterPlaybackSettingsOpt prevAfterPlaybackOpt;

        #endregion

        #region Properties
        public static Color StatusHighlightColor { get; set; }
        public static int IconSize { get; set; }
        public List<PlaylistItem> Playlist { get; set; }
        public PlaylistItem CurrentItem { get; set; }
        public static int PlaylistCount { get; set; }
        public List<string> RegexList { get; set; }
        public Point WindowPosition { get; set; }
        public Size WindowSize { get; set; }
        public bool RememberWindowPosition { get; set; }
        public bool RememberWindowSize { get; set; }
        public bool ShowToolTips { get; set; }
        public bool SnapWithPlayer { get; set; }
        public bool KeepSnapped { get; set; }
        public bool LockWindowSize { get; set; }
        public bool BeginPlaybackOnStartup { get; set; }
        public bool StripDirectoryInFileName { get; set; }
        public AfterPlaybackSettingsAction AfterPlaybackAction { get; set; }
        public List<string> Columns { get; set; }
        public List<string> TempRememberedFiles { get; set; }
        public string Theme { get; set; }

        #endregion

        #region PlaylistForm init and dispose

        public PlaylistForm()
        {
            InitializeComponent();
            Opacity = MIN_OPACITY;
        }

        public void Setup(Playlist playListUi)
        {
            if (Playlist != null) return;

            m_PlayListUi = playListUi;
            Icon = Gui.Icon;
            DoubleBuffered = true;

            Task.Factory.StartNew(LoadCustomSettings);

            Load += PlaylistForm_Load;
            Shown += PlaylistForm_Shown;

            OnRegexChange += PlaylistForm_OnRegexChange;

            dgv_PlayList.RowPrePaint += dgv_PlayList_RowPrePaint;
            dgv_PlayList.CellFormatting += dgv_PlayList_CellFormatting;
            dgv_PlayList.CellPainting += dgv_PlayList_CellPainting;
            dgv_PlayList.CellDoubleClick += dgv_PlayList_CellDoubleClick;
            dgv_PlayList.CellEndEdit += dgv_PlayList_CellEndEdit;
            dgv_PlayList.EditingControlShowing += dgv_PlayList_EditingControlShowing;
            dgv_PlayList.CellMouseEnter += dgv_PlayList_CellMouseEnter;
            dgv_PlayList.CellMouseLeave += dgv_PlayList_CellMouseLeave;
            dgv_PlayList.MouseMove += dgv_PlayList_MouseMove;
            dgv_PlayList.MouseDown += dgv_PlayList_MouseDown;
            dgv_PlayList.MouseUp += dgv_PlayList_MouseUp;
            dgv_PlayList.KeyDown += dgv_Playlist_KeyDown;
            dgv_PlayList.DragOver += dgv_PlayList_DragOver;
            dgv_PlayList.DragDrop += dgv_PlayList_DragDrop;
            dgv_PlayList.RowsAdded += dgv_PlayList_RowsAdded;
            dgv_PlayList.RowsRemoved += dgv_PlayList_RowsRemoved;
            dgv_PlayList.SelectionChanged += dgv_PlayList_SelectionChanged;
            dgv_PlayList.ColumnStateChanged += dgv_PlayList_ColumnStateChanged;

            Player.StateChanged += PlayerStateChanged;
            Player.Playback.Completed += PlaybackCompleted;
            Media.Frame.Decoded += FrameDecoded;
            Media.Frame.Presented += FramePresented;
            Player.FullScreenMode.Entering += EnteringFullScreenMode;
            Player.FullScreenMode.Exited += ExitedFullScreenMode;

            Playlist = new List<PlaylistItem>();
            TempRememberedFiles = new List<string>();

            ThreadPool.GetMaxThreads(out m_MinWorker, out m_MinIoc);
            ThreadPool.SetMinThreads(m_MinWorker, m_MinIoc);

            SetControlStates();
            DisableTabStop(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();

                if (Playlist != null)
                {
                    Player.StateChanged -= PlayerStateChanged;
                    Player.Playback.Completed -= PlaybackCompleted;
                    Media.Frame.Decoded -= FrameDecoded;
                    Media.Frame.Presented -= FramePresented;
                    Player.FullScreenMode.Entering -= EnteringFullScreenMode;
                    Player.FullScreenMode.Exited -= ExitedFullScreenMode;
                }
            }

            base.Dispose(disposing);
        }

        protected override void WndProc(ref Message message)
        {
            const int WM_SYSCOMMAND = 0x0112;
            const int SC_MOVE = 0xF010;

            switch (message.Msg)
            {
                case WM_SYSCOMMAND:
                    int command = message.WParam.ToInt32() & 0xfff0;
                    if (KeepSnapped && command == SC_MOVE) return;
                    break;
            }

            base.WndProc(ref message);
        }

        public void Show(Control owner)
        {
            if (Player.FullScreenMode.Active) return;

            Hide();
            SetLocation(owner);
            timer.Enabled = true;
            dgv_PlayList.Focus();
            base.Show(owner);
        }

        private void SetLocation(Control owner)
        {
            int borderWidth = SystemInformation.SizingBorderWidth;
            int right = Player.ActiveForm.Right;
            int top = Player.ActiveForm.Top;
            int width = Player.ActiveForm.Width;
            int height = Player.ActiveForm.Height;

            if (RememberWindowPosition && RememberWindowSize)
            {
                if (m_FirstShow)
                {
                    Location = WindowPosition;
                    Size = WindowSize;
                    m_FirstShow = false;
                }
            }
            else
            {
                if (RememberWindowPosition)
                {
                    if (m_FirstShow)
                    {
                        Location = WindowPosition;
                        m_FirstShow = false;
                    }
                }
                else
                {
                    if (LockWindowSize)
                    {
                        Left = right + borderWidth;
                        Top = top + borderWidth;
                    }
                    else
                    {
                        Left = right;
                        Top = top;
                    }
                }
                if (RememberWindowSize)
                {
                    if (m_FirstShow)
                    {
                        Size = WindowSize;
                        m_FirstShow = false;
                    }
                }
                else
                {
                    bool mpdnRememberBounds = Player.Config.Settings.GeneralSettings.RememberWindowSizePos;
                    var mpdnBounds = Player.Config.Settings.GeneralSettings.WindowBounds;

                    var screen = Screen.FromControl(owner);
                    var screenBounds = screen.WorkingArea;

                    if (mpdnRememberBounds)
                    {
                        Width = mpdnBounds.Right + mpdnBounds.Width >= (screenBounds.Width / 2)
                            ? screenBounds.Width - (mpdnBounds.Width + mpdnBounds.Left)
                            : Width;
                    }
                    else Width = right + width >= (screenBounds.Width / 2) ? (screenBounds.Width / 2) - width / 2 : Width;

                    if (LockWindowSize)
                    {
                        Width = Width - borderWidth;
                        Height = height - (borderWidth * 2);
                    }
                    else Height = height;
                }
            }

            if (SnapWithPlayer) m_PlayListUi.SnapPlayer();
        }

        public DataGridView GetDgvPlaylist()
        {
            return dgv_PlayList;
        }

        #endregion

        #region Playlist Methods

        public void PopulatePlaylist()
        {
            bool hasShownException = false;
            int prevScrollIndex = dgv_PlayList.FirstDisplayedScrollingRowIndex;

            dgv_PlayList.Rows.Clear();
            if (Playlist.Count == 0) return;

            var fileCount = 1;

            foreach (var i in Playlist)
            {
                string path = PathHelper.GetDirectoryName(i.FilePath);
                string directory = path.Substring(path.LastIndexOf("\\", StringComparison.Ordinal) + 1);
                string file = Path.GetFileName(i.FilePath);

                if (RegexList != null && RegexList.Count > 0)
                {
                    var count = 1;

                    try
                    {
                        foreach (string t in RegexList)
                        {
                            if (t.Equals("-") || t.Equals("_") || t.Equals("\\."))
                            {
                                file = Regex.Replace(file, t, " ", RegexOptions.Compiled);
                                file = Regex.Replace(file, @"\s+", " ", RegexOptions.Compiled).Trim();
                            }
                            else
                            {
                                var matches = Regex.Matches(file, t, RegexOptions.Compiled);

                                foreach (Match match in matches)
                                {
                                    int offset = match.Index == 0 ? 0 : 1;
                                    if (file.Substring(match.Index - offset, 1).Contains(" ")) file = file.Remove(match.Index - offset, 1);
                                }

                                file = Regex.Replace(file, t, string.Empty, RegexOptions.Compiled).Trim();
                            }

                            count++;
                        }
                    }
                    catch (Exception)
                    {
                        if (!hasShownException)
                        {
                            MessageBox.Show(
                                "Error evaluating expression at 'Regex " + count + "'!", "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);

                            hasShownException = true;
                        }
                    }
                }

                if (StripDirectoryInFileName && Playlist.Count > 1) if (file.Contains(directory)) file = file.Replace(directory, string.Empty).Trim();

                if (i.SkipChapters != null)
                {
                    if (i.EndChapter != -1)
                    {
                        dgv_PlayList.Rows.Add(string.Empty, fileCount, path, directory, file,
                            String.Join(",", i.SkipChapters),
                            i.EndChapter, i.Duration);
                    }
                    else
                    {
                        dgv_PlayList.Rows.Add(string.Empty, fileCount, path, directory, file,
                            String.Join(",", i.SkipChapters), null, i.Duration);
                    }
                }
                else dgv_PlayList.Rows.Add(string.Empty, fileCount, path, directory, file, null, null, i.Duration);

                fileCount++;
            }

            if (prevScrollIndex > -1) dgv_PlayList.FirstDisplayedScrollingRowIndex = prevScrollIndex;
            m_CurrentPlayIndex = (Playlist.FindIndex(i => i.Active) > -1)
                ? Playlist.FindIndex(i => i.Active)
                : m_CurrentPlayIndex != -1 && m_CurrentPlayIndex < Playlist.Count ? m_CurrentPlayIndex : -1;

            SetPlayStyling();

            NotifyPlaylistChanged();
            PlaylistCount = Playlist.Count;
        }

        public void ResetPlayCount()
        {
            foreach (var i in Playlist)
            {
                i.PlayCount = 0;
            }
        }

        public void RefreshPlaylist()
        {
            dgv_PlayList.Invalidate();
        }

        public void NewPlaylist(bool closeMedia = false)
        {
            ClearPlaylist();
            PopulatePlaylist();
            CurrentItem = null;
            m_CurrentPlayIndex = -1;
            Text = "Playlist";
            dgv_PlayList.Invalidate();

            if (closeMedia) CloseMedia();
        }

        public void ClearPlaylist()
        {
            Playlist.Clear();
            m_CurrentPlayIndex = -1;
            playToolStripMenuItem.Text = "Play";
        }

        public void OpenPlaylist(bool clear = true)
        {
            openPlaylistDialog.FileName = savePlaylistDialog.FileName;
            if (openPlaylistDialog.ShowDialog(Player.ActiveForm) != DialogResult.OK) return;

            m_LoadedPlaylist = openPlaylistDialog.FileName;
            OpenPlaylist(openPlaylistDialog.FileName, clear);
        }

        public void OpenPlaylist(string fileName, bool clear = true)
        {
            if (clear) ClearPlaylist();

            try
            {
                using (var sr = new StreamReader(fileName))
                {
                    string line;

                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line.Contains("|")) ParseWithChapters(line);
                        else ParseWithoutChapters(line);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Invalid or corrupt playlist file.\nAdditional info: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            PopulatePlaylist();

            if (clear) PlayActive();
        }

        private void SavePlaylist()
        {
            if (String.IsNullOrEmpty(m_LoadedPlaylist)) return;
            SavePlaylist(m_LoadedPlaylist);
        }

        public void SavePlaylist(string filename)
        {
            IEnumerable<string> playlist;
            var containsChapter = false;

            foreach (var item in Playlist.Where(item => item.HasChapter))
            {
                containsChapter = true;
            }

            if (containsChapter)
            {
                playlist =
                    Playlist
                        .Select(
                            item =>
                                string.Format("{0}{1} | SkipChapter: {2} | EndChapter: {3}",
                                    item.Active ? ACTIVE_INDICATOR : INACTIVE_INDICATOR,
                                    item.FilePath, item.HasChapter ? String.Join(",", item.SkipChapters) : "0",
                                    item.EndChapter > -1 ? item.EndChapter : 0));
            }
            else
            {
                playlist =
                    Playlist
                        .Select(
                            item =>
                                string.Format("{0}{1}", item.Active ? ACTIVE_INDICATOR : INACTIVE_INDICATOR,
                                    item.FilePath));
            }

            File.WriteAllLines(filename, playlist, Encoding.UTF8);

            var t = new ToolTip();
            t.Show("Playlist saved!", this, PointToClient(Cursor.Position), 2000);
        }

        public void SavePlaylistAs()
        {
            if (Playlist.Count == 0) return;

            savePlaylistDialog.FileName = openPlaylistDialog.FileName;
            if (savePlaylistDialog.ShowDialog(Player.ActiveForm) != DialogResult.OK) return;

            SavePlaylist(savePlaylistDialog.FileName);
        }

        #endregion

        #region Parsing Methods

        private void ParseChapterInput()
        {
            if (!SkipChapters.Visible && !EndChapter.Visible) return;

            try
            {
                for (var i = 0; i < dgv_PlayList.Rows.Count; i++)
                {
                    var skipChapterCell = dgv_PlayList.Rows[i].Cells[m_SkipCellIndex];
                    var endChapterCell = dgv_PlayList.Rows[i].Cells[m_EndCellIndex];

                    if (skipChapterCell.Value != null && skipChapterCell.Value.ToString() != string.Empty)
                    {
                        string formattedValue = Regex.Replace(skipChapterCell.Value.ToString(), @"[^0-9,\s]*",
                            string.Empty);
                        var numbers = formattedValue.Trim().Replace(" ", ",").Split(',');
                        var sortedNumbers =
                            numbers.Distinct().Except(new[] {string.Empty}).Select(int.Parse).OrderBy(x => x).ToList();

                        if (CurrentItem != null && i == m_CurrentPlayIndex)
                        {
                            if (sortedNumbers.Any(num => num >= Media.Chapters.Count))
                            {
                                if (Media.Chapters.Count == 0) ShowCellTooltip(skipChapterCell, "This file has no chapters");
                                else
                                {
                                    ShowCellTooltip(skipChapterCell,
                                        "Only numbers < " + Media.Chapters.Count + " are allowed");
                                }

                                sortedNumbers.RemoveAll(num => num >= Media.Chapters.Count);
                            }
                            if (Media.Chapters.Count == 0) sortedNumbers.Clear();
                        }

                        formattedValue = String.Join(",", sortedNumbers);
                        skipChapterCell.Value = formattedValue;
                    }

                    if (endChapterCell.Value != null && endChapterCell.Value.ToString() != string.Empty)
                    {
                        var value = new String(endChapterCell.Value.ToString().Where(Char.IsDigit).ToArray());

                        if (CurrentItem != null && i == m_CurrentPlayIndex)
                        {
                            if (value.Length > 0 && int.Parse(value) > Media.Chapters.Count)
                            {
                                if (Media.Chapters.Count == 0) ShowCellTooltip(endChapterCell, "This file has no chapters");
                                else
                                {
                                    ShowCellTooltip(endChapterCell,
                                        "Only numbers <= " + Media.Chapters.Count + " are allowed");
                                }

                                value = Media.Chapters.Count.ToString(CultureInfo.CurrentUICulture);
                            }
                            if (Media.Chapters.Count == 0) value = string.Empty;
                        }

                        endChapterCell.Value = value;
                    }
                }

                UpdatePlaylist();
            }
            catch (Exception ex)
            {
                Player.HandleException(ex);
            }
        }

        private void ParseWithoutChapters(string line)
        {
            string title;
            var isActive = false;

            if (line.StartsWith(ACTIVE_INDICATOR))
            {
                title = line.Substring(ACTIVE_INDICATOR.Length).Trim();
                isActive = true;
            }
            else if (line.StartsWith(INACTIVE_INDICATOR)) title = line.Substring(INACTIVE_INDICATOR.Length).Trim();
            else throw new FileLoadException();

            var item = new PlaylistItem(title, isActive);
            Playlist.Add(item);

            if (!Duration.Visible) return;
            Task.Factory.StartNew(GetMediaDuration);
        }

        private void ParseWithChapters(string line)
        {
            var splitLine = line.Split('|');
            string title;
            bool isActive;
            var skipChapters = new List<int>();

            if (splitLine[0].StartsWith(ACTIVE_INDICATOR))
            {
                title = splitLine[0].Substring(ACTIVE_INDICATOR.Length).Trim();
                isActive = true;
            }
            else if (line.StartsWith(INACTIVE_INDICATOR))
            {
                title = splitLine[0].Substring(INACTIVE_INDICATOR.Length).Trim();
                isActive = false;
            }
            else throw new FileLoadException();

            if (splitLine[1].Length > 0)
            {
                splitLine[1] = splitLine[1].Substring(splitLine[1].IndexOf(':') + 1).Trim();
                skipChapters = new List<int>(splitLine[1].Split(',').Select(int.Parse));
            }

            int endChapter = int.Parse(splitLine[2].Substring(splitLine[2].IndexOf(':') + 1).Trim());
            Playlist.Add(new PlaylistItem(title, skipChapters, endChapter, isActive));

            if (!Duration.Visible) return;
            Task.Factory.StartNew(GetMediaDuration);
        }

        private void UpdatePlaylist()
        {
            try
            {
                for (var i = 0; i < dgv_PlayList.Rows.Count; i++)
                {
                    var skipChapters = new List<int>();
                    int endChapter = -1;

                    var skipChapterCell = dgv_PlayList.Rows[i].Cells[m_SkipCellIndex];
                    var endChapterCell = dgv_PlayList.Rows[i].Cells[m_EndCellIndex];

                    if (skipChapterCell.Value != null && skipChapterCell.Value.ToString() != string.Empty)
                    {
                        skipChapters = skipChapterCell.Value.ToString().Split(',').Select(int.Parse).ToList();
                        Playlist.ElementAt(i).HasChapter = true;
                    }

                    if (endChapterCell.Value != null && endChapterCell.Value.ToString() != string.Empty) endChapter = int.Parse(endChapterCell.Value.ToString());

                    Playlist.ElementAt(i).SkipChapters = skipChapters;
                    Playlist.ElementAt(i).EndChapter = endChapter;
                }
            }
            catch (Exception ex)
            {
                Player.HandleException(ex);
            }
        }

        #endregion

        #region Media Methods

        public void PlayActive()
        {
            m_CurrentPlayIndex = -1;

            foreach (var item in Playlist)
            {
                m_CurrentPlayIndex++;
                if (!item.Active) continue;
                OpenMedia();
                return;
            }

            m_CurrentPlayIndex = 0;
            OpenMedia();
        }

        public void PlayNext(bool incIdx = true)
        {
            if (incIdx) m_CurrentPlayIndex++;

            if (m_CurrentPlayIndex > Playlist.Count - 1)
            {
                m_CurrentPlayIndex = Playlist.Count - 1;
                return;
            }

            SetPlayStyling();
            OpenMedia();
        }

        public void PlayPrevious()
        {
            m_CurrentPlayIndex--;

            if (m_CurrentPlayIndex < 0)
            {
                m_CurrentPlayIndex = 0;
                return;
            }

            SetPlayStyling();
            OpenMedia();
        }

        public void SetPlaylistIndex(int index)
        {
            m_CurrentPlayIndex = index;
            SetPlayStyling();
            OpenMedia();
        }

        private void PlaySelectedFile()
        {
            if (dgv_PlayList.Rows.Count < 1 || dgv_PlayList.CurrentRow == null) return;
            SetPlaylistIndex(dgv_PlayList.CurrentRow.Index);
        }

        public void PlayNextFileInDirectory(bool next = true)
        {
            if (Player.State == PlayerState.Closed) return;

            string mediaPath = Media.FilePath;
            string mediaDir = Path.GetDirectoryName(mediaPath);
            var mediaFiles = m_PlayListUi.GetMediaFiles(mediaDir);
            string nextFile = next
                ? mediaFiles.SkipWhile(file => file != mediaPath).Skip(1).FirstOrDefault()
                : mediaFiles.TakeWhile(file => file != mediaPath).LastOrDefault();

            if (nextFile == null) return;

            Media.Open(nextFile);

            if (Playlist.Count == 1) ActiveFile(nextFile);
            else OpenFiles(new[] {nextFile});
        }

        private void OpenMedia(bool queue = false)
        {
            if (m_CurrentPlayIndex < 0 || m_CurrentPlayIndex >= Playlist.Count) return;

            bool playerWasFullScreen = Player.FullScreenMode.Active;
            ResetActive();

            try
            {
                var item = Playlist[m_CurrentPlayIndex];
                dgv_PlayList.CurrentCell = dgv_PlayList.Rows[m_CurrentPlayIndex].Cells[m_TitleCellIndex];

                if (File.Exists(item.FilePath))
                {
                    Media.Open(item.FilePath, !queue);
                    SetPlayStyling();
                }
                else
                {
                    if (m_CurrentPlayIndex != Playlist.Count - 1) PlayNext();
                    else CloseMedia();

                    SetPlayStyling();
                    return;
                }

                if (playerWasFullScreen) Player.FullScreenMode.Active = true;

                item.Active = true;
                CurrentItem = item;
                m_PreviousChapterPosition = 0;

                if (!queue) Text = Player.State + " ─ " + CurrentItem.FilePath;

                ParseChapterInput();
            }
            catch (Exception ex)
            {
                Player.HandleException(ex);
                PlayNext();
            }

            dgv_PlayList.Invalidate();

            if (string.IsNullOrEmpty(Media.FilePath)) return;
            if (!Duration.Visible) return;
            Task.Factory.StartNew(GetCurrentMediaDuration);
        }

        public void CloseMedia()
        {
            CurrentItem = null;
            m_CurrentPlayIndex = -1;
            Text = "Playlist";
            Media.Close();
            dgv_PlayList.Invalidate();
        }

        #endregion

        #region The Methods

        public void InsertFile(int index, string fileName)
        {
            var item = new PlaylistItem(fileName, false);
            Playlist.Insert(index, item);
            PopulatePlaylist();
        }

        public void AddFiles(string[] fileNames)
        {
            AddFilesToPlaylist(fileNames);
            if (Player.State == PlayerState.Playing || Player.State == PlayerState.Paused) return;
            m_CurrentPlayIndex = fileNames.Count() > 1 ? Playlist.Count - fileNames.Count() : Playlist.Count - 1;
            SetPlayStyling();
            OpenMedia(true);
        }

        public void ActiveFile(string fileName)
        {
            ResetActive();
            var item = new PlaylistItem(fileName, true);
            ClearPlaylist();
            Playlist.Add(item);
            CurrentItem = item;
            PopulatePlaylist();

            Text = Player.State + " ─ " + CurrentItem.FilePath;

            if (!Duration.Visible) return;
            Task.Factory.StartNew(GetCurrentMediaDuration);
        }

        private void AddFilesToPlaylist(IEnumerable<string> fileNames)
        {
            foreach (var item in fileNames.Select(s => new PlaylistItem(s, false) {EndChapter = -1}))
            {
                Playlist.Add(item);
            }

            if (dgv_PlayList.CurrentRow != null) m_SelectedRowIndex = dgv_PlayList.CurrentRow.Index;

            PopulatePlaylist();

            if (m_SelectedRowIndex < 0) m_SelectedRowIndex = 0;
            else if (m_SelectedRowIndex > Playlist.Count - 1) m_SelectedRowIndex = Playlist.Count - 1;

            dgv_PlayList.CurrentCell = dgv_PlayList.Rows[m_SelectedRowIndex].Cells[m_TitleCellIndex];

            if (!Duration.Visible) return;
            Task.Factory.StartNew(GetMediaDuration);
        }

        private void AddFolderToPlaylist()
        {
            using (var fd = new VistaFolderBrowserDialog())
            {
                fd.Description = "Add folder to playlist";
                fd.UseDescriptionForTitle = true;
                fd.ShowNewFolderButton = true;

                if (fd.ShowDialog(this) != DialogResult.OK) return;

                var media = m_PlayListUi.GetAllMediaFiles(fd.SelectedPath).ToArray();
                if (media.Length == 0)
                {
                    MessageBox.Show("There are no files in the selected directory.", "Warning", MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                AddFiles(media);
            }
        }

        private void AddClipboardToPlaylist()
        {
            var files = Clipboard.GetText().Replace("\r", string.Empty).Split('\n').ToList();
            files.RemoveAll(f => !File.Exists(f));

            if (files.Count < 1) return;
            AddFiles(files.ToArray());
        }

        public void OpenFiles(string[] fileNames)
        {
            AddFilesToPlaylist(fileNames);
            m_CurrentPlayIndex = fileNames.Count() > 1 ? Playlist.Count - fileNames.Count() : Playlist.Count - 1;
            OpenMedia();
        }

        private void OpenFolder()
        {
            using (var fd = new VistaFolderBrowserDialog())
            {
                fd.Description = "Open and play folder";
                fd.UseDescriptionForTitle = true;
                fd.ShowNewFolderButton = true;

                if (fd.ShowDialog(this) != DialogResult.OK) return;

                ClearPlaylist();

                var media = m_PlayListUi.GetAllMediaFiles(fd.SelectedPath).ToArray();
                if (media.Length == 0)
                {
                    MessageBox.Show("There are no files in the selected directory.", "Warning", MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                OpenFiles(media);
            }
        }

        private void OpenClipboard()
        {
            ClearPlaylist();

            var files = Clipboard.GetText().Replace("\r", string.Empty).Split('\n').ToList();
            files.RemoveAll(f => !File.Exists(f));

            if (files.Count < 1) return;
            OpenFiles(files.ToArray());
        }

        public void RemoveFile(int index)
        {
            Playlist.RemoveAt(index);
            if (index == Playlist.Count) CloseMedia();
            PopulatePlaylist();
        }

        private void RemoveSelectedItems()
        {
            var rowIndexes = new List<int>();

            try
            {
                if (Playlist.Count <= 0) return;
                if (dgv_PlayList.CurrentRow != null) m_SelectedRowIndex = dgv_PlayList.CurrentRow.Index;

                rowIndexes.AddRange(from DataGridViewRow r in dgv_PlayList.SelectedRows select r.Index);

                foreach (int index in rowIndexes.OrderByDescending(v => v))
                {
                    if (index == m_CurrentPlayIndex) CloseMedia();

                    Playlist.RemoveAt(index);
                }

                PopulatePlaylist();

                m_SelectedRowIndex = m_SelectedRowIndex < 0
                    ? 0
                    : m_SelectedRowIndex > Playlist.Count - 1 ? Playlist.Count - 1 : m_SelectedRowIndex;

                dgv_PlayList.CurrentCell = Playlist.Count > 0
                    ? dgv_PlayList.Rows[m_SelectedRowIndex].Cells[m_TitleCellIndex]
                    : dgv_PlayList.CurrentCell = null;
            }
            catch (Exception ex)
            {
                Player.HandleException(ex);
            }
        }

        private void RemoveUnselectedItems()
        {
            var rowIndexes = new List<int>();

            try
            {
                if (Playlist.Count <= 0) return;
                if (dgv_PlayList.CurrentRow != null) m_SelectedRowIndex = dgv_PlayList.CurrentRow.Index;

                rowIndexes.AddRange(
                    dgv_PlayList.Rows.Cast<DataGridViewRow>().Where(r1 => !r1.Selected).Select(r2 => r2.Index));

                foreach (int index in rowIndexes.OrderByDescending(v => v))
                {
                    if (index == m_CurrentPlayIndex) CloseMedia();

                    Playlist.RemoveAt(index);
                }

                PopulatePlaylist();

                m_SelectedRowIndex = m_SelectedRowIndex < 0
                    ? 0
                    : m_SelectedRowIndex > Playlist.Count - 1 ? Playlist.Count - 1 : m_SelectedRowIndex;

                dgv_PlayList.CurrentCell = Playlist.Count > 0
                    ? dgv_PlayList.Rows[m_SelectedRowIndex].Cells[m_TitleCellIndex]
                    : dgv_PlayList.CurrentCell = null;
            }
            catch (Exception ex)
            {
                Player.HandleException(ex);
            }
        }

        private void RemoveNonExistentItems()
        {
            try
            {
                if (Playlist.Count <= 0) return;
                if (dgv_PlayList.CurrentRow != null) m_SelectedRowIndex = dgv_PlayList.CurrentRow.Index;

                Playlist.RemoveAll(p => !File.Exists(p.FilePath));

                PopulatePlaylist();

                m_SelectedRowIndex = m_SelectedRowIndex < 0
                    ? 0
                    : m_SelectedRowIndex > Playlist.Count - 1 ? Playlist.Count - 1 : m_SelectedRowIndex;

                dgv_PlayList.CurrentCell = Playlist.Count > 0
                    ? dgv_PlayList.Rows[m_SelectedRowIndex].Cells[m_TitleCellIndex]
                    : dgv_PlayList.CurrentCell = null;
            }
            catch (Exception ex)
            {
                Player.HandleException(ex);
            }
        }

        private void ViewFileLocation()
        {
            if (Playlist.Count == 0) return;
            if (dgv_PlayList.SelectedRows.Count == 0) return;

            foreach (DataGridViewRow r in dgv_PlayList.SelectedRows)
            {
                string media = Playlist[r.Index].FilePath;
                if (!File.Exists(media)) continue;
                Process.Start(PathHelper.GetDirectoryName(media));
            }
        }

        private void ViewMediaInfo()
        {
            if (Playlist.Count == 0) return;
            if (dgv_PlayList.SelectedRows.Count == 0) return;

            foreach (DataGridViewRow r in dgv_PlayList.SelectedRows)
            {
                string media = Playlist[r.Index].FilePath;
                if (!File.Exists(media)) continue;
                var mediaInfo = new ViewMediaInfoForm(media);
                mediaInfo.Show();
            }
        }

        private void SortPlayList(bool ascending = true)
        {
            RememberPlaylist();

            if (ascending)
            {
                Playlist = Playlist.OrderBy(f => Path.GetDirectoryName(f.FilePath), new NaturalSortComparer())
                    .ThenBy(f => Path.GetFileName(f.FilePath), new NaturalSortComparer()).ToList();
            }
            else
            {
                Playlist = Playlist.OrderByDescending(f => Path.GetDirectoryName(f.FilePath), new NaturalSortComparer())
                    .ThenByDescending(f => Path.GetFileName(f.FilePath), new NaturalSortComparer()).ToList();
            }

            PopulatePlaylist();
        }

        public void RememberPlaylist()
        {
            TempRememberedFiles.Clear();
            if (Playlist.Count <= 0) return;

            foreach (var i in Playlist)
            {
                string skipChapters = string.Empty;

                if (i.SkipChapters != null && i.SkipChapters.Count > 0) skipChapters = string.Join(",", i.SkipChapters);

                TempRememberedFiles.Add(i.FilePath + "|" + skipChapters + "|" + i.EndChapter + "|" +
                                        i.Active + "|" + i.Duration + "|" + i.PlayCount);
            }
        }

        public void RestoreRememberedPlaylist()
        {
            if (TempRememberedFiles.Count == 0) return;

            var playList = new List<PlaylistItem>();

            foreach (string f in TempRememberedFiles)
            {
                var s = f.Split('|');
                string filePath = s[0];
                var skipChapters = new List<int>();
                if (s[1].Length > 0)
                {
                    if (s[1].Contains(",")) skipChapters = s[1].Split(',').Select(int.Parse).ToList();
                    else skipChapters.Add(int.Parse(s[1]));
                }
                int endChapter = int.Parse(s[2]);
                bool active = Boolean.Parse(s[3]);
                string duration = s[4];
                int playCount = int.Parse(s[5]);

                playList.Add(new PlaylistItem(filePath, skipChapters, endChapter, active, duration, playCount));
            }

            Playlist = playList;
            PopulatePlaylist();
            RefreshPlaylist();
        }

        private void ShufflePlayList()
        {
            RememberPlaylist();
            Playlist.Shuffle();
            PopulatePlaylist();
        }

        #endregion

        #region Controls Handling Methods

        public void LoadCustomSettings()
        {
            m_FontColor = Color.Empty;
            m_FontDropShadowColor = Color.Empty;
            m_SelectionFontColor = Color.Empty;
            m_PlayFontColor = Color.Empty;
            m_ColumnHeaderFontColor = Color.Empty;
            m_FormColor = Color.Empty;
            m_SelectionColor = Color.Empty;
            m_GreyOutColor = Color.Empty;
            m_PlayColor = Color.Empty;
            m_ColumnHeaderColor = Color.Empty;
            m_ColumnHeaderBorderColor = Color.Empty;
            StatusHighlightColor = Color.Empty;
            m_StatusBorderColor = Color.Empty;
            m_ColumnHeaderTransparency = false;
            m_DropShadow = false;

            if (!Directory.Exists(PLAYLIST_ICONS_DIR)) return;

            int loadedIcons = 0;

            string[] icons =
            {
                "buttonAdd.png",
                "buttonDel.png",
                "buttonAddFolder.png",
                "buttonLeft.png",
                "buttonRight.png",
                "buttonSortAscending.png",
                "buttonSortDescending.png",
                "buttonShuffle.png",
                "buttonRepeatPlaylist.png",
                "buttonRestore.png",
                "buttonNewPlaylist.png",
                "buttonOpenPlaylist.png",
                "buttonSavePlaylist.png",
                "buttonSettings.png"
            };

            foreach (string i in icons)
            {
                int dotIdx = i.IndexOf('.');
                var c = statusStrip1.Items.Find(i.Substring(0, dotIdx), true).FirstOrDefault();
                if (!File.Exists(PLAYLIST_ICONS_DIR + @"\" + Theme + @"\" + i))
                {
                    c.Visible = false;
                    continue;
                }
                if (c == null) continue;
                c.Visible = true;
                c.Width = IconSize + 8;
                c.Height = IconSize + 9;
                c.Image = BitmapHelper.Resize(PLAYLIST_ICONS_DIR + @"\" + Theme + @"\" + i, IconSize, IconSize);
                c.ImageAlign = ContentAlignment.MiddleCenter;
                loadedIcons++;
            }

            GuiThread.DoAsync(() =>
            {
                statusStrip1.Visible = loadedIcons > 0;
                statusStrip1.Height = IconSize;

                int minWidth = Convert.ToInt32(GetDpi() + (IconSize * 14) + 75);

                switch (m_PlayListUi.Settings.IconScale)
                {
                    case IconScale.Scale100X:
                        MinimumSize = new Size(minWidth, 115);
                        break;
                    case IconScale.Scale125X:
                        MinimumSize = new Size(minWidth, 115);
                        break;
                    case IconScale.Scale150X:
                        MinimumSize = new Size(minWidth, 115);
                        break;
                    case IconScale.Scale175X:
                        MinimumSize = new Size(minWidth, 115);
                        break;
                    case IconScale.Scale200X:
                        MinimumSize = new Size(minWidth, 115);
                        break;
                }
            });

            const string backgroundImage = "background";
            LoadCustomBackground(backgroundImage);

            const string styleFile = "theme.style";
            LoadCustomTheme(styleFile);

            GuiThread.DoAsync(() =>
            {
                if (m_ColumnHeaderTransparency) dgv_PlayList.SetColumnHeaderTransparent();
                else dgv_PlayList.ResetColumnHeader();

                statusStrip1.BorderColor = m_StatusBorderColor;
                if (m_FormColor != Color.Empty) BackColor = m_FormColor;
                if (m_ColumnHeaderFontColor != Color.Empty) dgv_PlayList.ColumnHeadersDefaultCellStyle.ForeColor = m_ColumnHeaderFontColor;
                if (m_ColumnHeaderColor != Color.Empty) dgv_PlayList.ColumnHeadersDefaultCellStyle.BackColor = m_ColumnHeaderColor;
                if (m_FontColor != Color.Empty) dgv_PlayList.DefaultCellStyle.ForeColor = m_FontColor;
                if (m_SelectionColor != Color.Empty) dgv_PlayList.DefaultCellStyle.SelectionBackColor = m_SelectionColor;
                if (m_SelectionFontColor != Color.Empty) dgv_PlayList.DefaultCellStyle.SelectionForeColor = m_SelectionFontColor;

                SetPlayStyling();
                dgv_PlayList.Invalidate();
                dgv_PlayList.Refresh();
            });
        }

        private void LoadCustomBackground(string backgroundImage)
        {
            if (File.Exists(PLAYLIST_ICONS_DIR + @"\" + Theme + @"\" + backgroundImage + ".png"))
            {
                GuiThread.DoAsync(() =>
                {
                    dgv_PlayList.BackgroundImage =
                        Image.FromFile(PLAYLIST_ICONS_DIR + @"\" + Theme + @"\" + backgroundImage + ".png");
                    dgv_PlayList.SetCellsTransparent();
                });
            }
            else if (File.Exists(PLAYLIST_ICONS_DIR + @"\" + Theme + @"\" + backgroundImage + ".jpg"))
            {
                GuiThread.DoAsync(() =>
                {
                    dgv_PlayList.BackgroundImage =
                        Image.FromFile(PLAYLIST_ICONS_DIR + @"\" + Theme + @"\" + backgroundImage + ".jpg");
                    dgv_PlayList.SetCellsTransparent();
                });
            }
            else
            {
                GuiThread.DoAsync(() =>
                {
                    dgv_PlayList.BackgroundImage = null;
                    dgv_PlayList.ResetCells();
                });
            }
        }

        private void LoadCustomTheme(string styleFile)
        {
            try
            {
                if (File.Exists(PLAYLIST_ICONS_DIR + @"\" + Theme + @"\" + styleFile))
                {
                    using (var r = File.OpenText(PLAYLIST_ICONS_DIR + @"\" + Theme + @"\" + styleFile))
                    {
                        string line;
                        while ((line = r.ReadLine()) != null)
                        {
                            string[] colors = new string[16];

                            if (line.Contains(":")) colors = line.Split(':')[1].Split(',');

                            if (line.Contains("fontColor"))
                            {
                                m_FontColor = Color.FromArgb(int.Parse(colors[0]), int.Parse(colors[1]),
                                    int.Parse(colors[2]),
                                    int.Parse(colors[3]));
                            }
                            if (line.Contains("fontDropShadowColor"))
                            {
                                m_FontDropShadowColor = Color.FromArgb(int.Parse(colors[0]), int.Parse(colors[1]),
                                    int.Parse(colors[2]),
                                    int.Parse(colors[3]));
                                m_DropShadow = true;
                            }
                            if (line.Contains("selectionFontColor"))
                            {
                                m_SelectionFontColor = Color.FromArgb(int.Parse(colors[0]), int.Parse(colors[1]),
                                    int.Parse(colors[2]),
                                    int.Parse(colors[3]));
                            }
                            if (line.Contains("playFontColor"))
                            {
                                m_PlayFontColor = Color.FromArgb(int.Parse(colors[0]), int.Parse(colors[1]),
                                    int.Parse(colors[2]),
                                    int.Parse(colors[3]));
                            }
                            if (line.Contains("columnHeaderFontColor"))
                            {
                                m_ColumnHeaderFontColor = Color.FromArgb(int.Parse(colors[0]), int.Parse(colors[1]),
                                    int.Parse(colors[2]),
                                    int.Parse(colors[3]));
                            }
                            if (line.Contains("formColor"))
                            {
                                m_FormColor = Color.FromArgb(int.Parse(colors[0]), int.Parse(colors[1]),
                                    int.Parse(colors[2]),
                                    int.Parse(colors[3]));
                            }
                            if (line.Contains("selectionColor"))
                            {
                                m_SelectionColor = Color.FromArgb(int.Parse(colors[0]), int.Parse(colors[1]),
                                    int.Parse(colors[2]),
                                    int.Parse(colors[3]));
                            }
                            if (line.Contains("greyOutColor"))
                            {
                                m_GreyOutColor = Color.FromArgb(int.Parse(colors[0]), int.Parse(colors[1]),
                                    int.Parse(colors[2]),
                                    int.Parse(colors[3]));
                            }
                            if (line.Contains("playColor"))
                            {
                                m_PlayColor = Color.FromArgb(int.Parse(colors[0]), int.Parse(colors[1]),
                                    int.Parse(colors[2]),
                                    int.Parse(colors[3]));
                            }
                            if (line.Contains("columnHeaderColor"))
                            {
                                m_ColumnHeaderColor = Color.FromArgb(int.Parse(colors[0]), int.Parse(colors[1]),
                                    int.Parse(colors[2]),
                                    int.Parse(colors[3]));
                            }
                            if (line.Contains("columnHeaderBorderColor"))
                            {
                                m_ColumnHeaderBorderColor = Color.FromArgb(int.Parse(colors[0]), int.Parse(colors[1]),
                                    int.Parse(colors[2]),
                                    int.Parse(colors[3]));
                            }
                            if (line.Contains("statusHighlightColor"))
                            {
                                StatusHighlightColor = Color.FromArgb(int.Parse(colors[0]), int.Parse(colors[1]),
                                    int.Parse(colors[2]),
                                    int.Parse(colors[3]));
                            }
                            if (line.Contains("statusBorderColor"))
                            {
                                m_StatusBorderColor = Color.FromArgb(int.Parse(colors[0]), int.Parse(colors[1]),
                                    int.Parse(colors[2]),
                                    int.Parse(colors[3]));
                            }
                            if (line.Contains("columnHeaderTransparency")) m_ColumnHeaderTransparency = bool.Parse(line.Split(':')[1]);
                        }
                    }
                }
            }
            catch (IndexOutOfRangeException ex)
            {
                MessageBox.Show("Error parsing '" + Theme + @"\" + styleFile + "'\n\nError: " + ex.Message + "\nThis is likely because you have an error in your theme file.\n\nMake sure the colors are in (a,r,g,b) format.", "Error parsing '" + Theme + @"\" + styleFile + "'", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void SetControlStates()
        {
            if (Playlist.Count > 1)
            {
                buttonLeft.Enabled = true;
                buttonRight.Enabled = true;
                buttonSortAscending.Enabled = true;
                buttonSortDescending.Enabled = true;
                buttonShuffle.Enabled = true;
                buttonRestore.Enabled = true;
                sortToolStripMenuItem.Enabled = true;
                ascendingToolStripMenuItem.Enabled = true;
                descendingToolStripMenuItem.Enabled = true;
                shuffleToolStripMenuItem.Enabled = true;
            }
            else
            {
                buttonLeft.Enabled = false;
                buttonRight.Enabled = false;
                buttonSortAscending.Enabled = false;
                buttonSortDescending.Enabled = false;
                buttonShuffle.Enabled = false;
                buttonRestore.Enabled = false;
                sortToolStripMenuItem.Enabled = false;
                ascendingToolStripMenuItem.Enabled = false;
                descendingToolStripMenuItem.Enabled = false;
                shuffleToolStripMenuItem.Enabled = false;
            }

            if (Playlist.Count > 0)
            {
                buttonNewPlaylist.Enabled = true;
                buttonSavePlaylist.Enabled = true;
                buttonDel.Enabled = true;
                newPlaylistToolStripMenuItem.Enabled = true;
                savePlaylistToolStripMenuItem.Enabled = true;
                savePlaylistAsToolStripMenuItem.Enabled = true;
                viewFileLocationToolStripMenuItem.Enabled = true;
                viewMediaInfoToolStripMenuItem.Enabled = true;
                playToolStripMenuItem.Enabled = true;
                removeSelectedItemsToolStripMenuItem.Enabled = true;
                removeUnselectedItemsToolStripMenuItem.Enabled = true;
                removeNonExistentItemsToolStripMenuItem.Enabled = true;
            }
            else
            {
                buttonNewPlaylist.Enabled = false;
                buttonSavePlaylist.Enabled = false;
                buttonDel.Enabled = false;
                newPlaylistToolStripMenuItem.Enabled = false;
                savePlaylistToolStripMenuItem.Enabled = false;
                savePlaylistAsToolStripMenuItem.Enabled = false;
                viewFileLocationToolStripMenuItem.Enabled = false;
                viewMediaInfoToolStripMenuItem.Enabled = false;
                playToolStripMenuItem.Enabled = false;
                removeSelectedItemsToolStripMenuItem.Enabled = false;
                removeUnselectedItemsToolStripMenuItem.Enabled = false;
                removeNonExistentItemsToolStripMenuItem.Enabled = false;
            }

            buttonRepeatPlaylist.Tag = m_PlayListUi.Settings.AfterPlaybackOpt == AfterPlaybackSettingsOpt.RepeatPlaylist ? "Enabled" : "Disabled";
            buttonRepeatPlaylist.Invalidate();
        }

        private void HandleContextMenu()
        {
            if (dgv_PlayList.Rows.Count < 1) return;
            if (dgv_PlayList.CurrentCell == null || dgv_PlayList.CurrentCell.RowIndex != m_CurrentPlayIndex) playToolStripMenuItem.Text = "Play";
            else
            {
                switch (Player.State)
                {
                    case PlayerState.Paused:
                        playToolStripMenuItem.Text = "Resume";
                        break;
                    case PlayerState.Playing:
                        playToolStripMenuItem.Text = "Pause";
                        break;
                    default:
                        playToolStripMenuItem.Text = "Play";
                        break;
                }
            }
        }

        #endregion

        #region Column Handling Methods

        public static void UpdatePlaylistWithRegexFilter(List<string> regexList, bool stripDirectory)
        {
            var args = new RegexEventArgs(regexList, stripDirectory);
            OnRegexChange(null, args);
        }

        private void SetColumnSize()
        {
            if (m_ColumnsFixed) return;
            if (Columns == null || Columns.Count == 0) return;

            for (var i = 0; i < dgv_PlayList.Columns.Count; i++)
            {
                var c = dgv_PlayList.Columns[i];
                var split = Columns[i].Split('|');
                if (split[0] != c.Name) continue;
                if (split[0] != "Title") c.Visible = Convert.ToBoolean(split[1]);

                c.Width = int.Parse(split[2]);
                c.FillWeight = int.Parse(split[2]);
            }

            m_ColumnsFixed = true;
        }

        private void FitColumnsToHeader()
        {
            var list = new int[7];

            for (var i = 1; i < dgv_PlayList.Columns.Count; i++)
            {
                var c = dgv_PlayList.Columns[i];
                c.AutoSizeMode = DataGridViewAutoSizeColumnMode.ColumnHeader;
                list[i - 1] = c.Width;
            }

            for (var i = 1; i < dgv_PlayList.Columns.Count; i++)
            {
                var c = dgv_PlayList.Columns[i];
                c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                c.MinimumWidth = list[i - 1];
            }
        }

        private void SetColumnStates()
        {
            numberToolStripMenuItem.Checked = Number.Visible;
            directoryToolStripMenuItem.Checked = CurrentDirectory.Visible;
            fullPathToolStripMenuItem.Checked = FullPath.Visible;
            skipChaptersToolStripMenuItem.Checked = SkipChapters.Visible;
            endChapterToolStripMenuItem.Checked = EndChapter.Visible;
            durationToolStripMenuItem.Checked = Duration.Visible;

            if (Title.Visible) m_TitleCellIndex = Title.Index;
            if (SkipChapters.Visible) m_SkipCellIndex = SkipChapters.Index;
            if (EndChapter.Visible) m_EndCellIndex = EndChapter.Index;
        }

        private void UpdateColumns(object sender, EventArgs e)
        {
            Number.Visible = numberToolStripMenuItem.Checked;
            FullPath.Visible = fullPathToolStripMenuItem.Checked;
            CurrentDirectory.Visible = directoryToolStripMenuItem.Checked;
            SkipChapters.Visible = skipChaptersToolStripMenuItem.Checked;
            EndChapter.Visible = endChapterToolStripMenuItem.Checked;
            Duration.Visible = durationToolStripMenuItem.Checked;

            if (Title.Visible) m_TitleCellIndex = Title.Index;
            if (SkipChapters.Visible) m_SkipCellIndex = SkipChapters.Index;
            if (EndChapter.Visible) m_EndCellIndex = EndChapter.Index;
        }

        #endregion

        #region Helper Methods

        private float GetDpi()
        {
            var g = Graphics.FromHwnd(IntPtr.Zero);
            return g.DpiX;
        }

        private void ResetActive()
        {
            foreach (var item in Playlist)
            {
                item.Active = false;
            }
        }

        public void SetPlayStyling()
        {
            foreach (DataGridViewRow r in dgv_PlayList.Rows)
            {
                if (File.Exists(Playlist[r.Index].FilePath))
                {
                    if (AfterPlaybackAction == AfterPlaybackSettingsAction.GreyOutFile &&
                        Playlist[r.Index].PlayCount > 0 && r.Index != m_CurrentPlayIndex)
                    {
                        var item = Playlist[r.Index];
                        if (20 + (item.PlayCount * 70) >= 180) r.DefaultCellStyle.ForeColor = Color.FromArgb(180, 180, 180);
                        else
                        {
                            r.DefaultCellStyle.ForeColor = Color.FromArgb(20 + (item.PlayCount * 70),
                                20 + (item.PlayCount * 70), 20 + (item.PlayCount * 70));
                        }
                        r.Selected = false;
                    }
                    else
                    {
                        var f = new Font(dgv_PlayList.DefaultCellStyle.Font, FontStyle.Regular);
                        r.DefaultCellStyle.Font = f;
                        r.DefaultCellStyle.ForeColor = m_FontColor;
                        r.Selected = false;
                    }
                }
                else
                {
                    var f = new Font(dgv_PlayList.DefaultCellStyle.Font, FontStyle.Strikeout);
                    r.DefaultCellStyle.Font = f;
                    r.DefaultCellStyle.ForeColor = m_GreyOutColor;
                }
            }

            if (m_CurrentPlayIndex == -1) return;
            dgv_PlayList.Rows[m_CurrentPlayIndex].Selected = true;
            SetInitialDirectory();

            if (string.IsNullOrEmpty(Media.FilePath)) return;
            var fnt = new Font(dgv_PlayList.DefaultCellStyle.Font, FontStyle.Regular);
            dgv_PlayList.Rows[m_CurrentPlayIndex].DefaultCellStyle.Font = fnt;
            dgv_PlayList.Rows[m_CurrentPlayIndex].DefaultCellStyle.ForeColor = m_PlayFontColor;
        }

        private static void SelectChapter(int chapterNum)
        {
            if (Player.State == PlayerState.Closed) return;

            var chapters = GetChapters().ToArray();

            if (chapters.ElementAt(chapterNum) == null) return;
            Media.Seek(chapters.ElementAt(chapterNum).Position);
            Player.OsdText.Show(chapters.ElementAt(chapterNum).Name);
        }

        private static int GetChapterIndexByPosition(long position)
        {
            var currentChapterIndex = 0;

            foreach (var c in GetChapters().Where(c => c != null))
            {
                currentChapterIndex++;
                if (c.Position != position) continue;
                return currentChapterIndex;
            }

            return 0;
        }

        private static IEnumerable<Chapter> GetChapters()
        {
            return Media.Chapters.OrderBy(chapter => chapter.Position);
        }

        private static void DisableTabStop(Control c)
        {
            if (c.GetType() == typeof(DataGridView)) return;
            c.TabStop = false;

            foreach (Control i in c.Controls)
            {
                DisableTabStop(i);
            }
        }

        private void SelectNextEditableCell()
        {
            var currentCell = dgv_PlayList.CurrentCell;
            if (currentCell == null) return;

            int nextRow = currentCell.RowIndex;
            var nextCell = SkipChapters.Visible
                ? dgv_PlayList.Rows[nextRow].Cells[m_SkipCellIndex]
                : dgv_PlayList.Rows[nextRow].Cells[m_EndCellIndex];

            if (nextCell == null || !nextCell.Visible) return;
            dgv_PlayList.CurrentCell = nextCell;
        }

        private void ShowCellTooltip(DataGridViewCell cell, string message)
        {
            var toolTip = new ToolTip();
            var cellDisplayRect = dgv_PlayList.GetCellDisplayRectangle(cell.ColumnIndex, cell.RowIndex, false);
            toolTip.Show(message, dgv_PlayList,
                cellDisplayRect.X + cell.Size.Width / 2,
                cellDisplayRect.Y + cell.Size.Height / 2,
                2000);
        }

        private void ShowCurrentCellTooltip(string message)
        {
            var toolTip = new ToolTip();
            var cell = dgv_PlayList.CurrentCell;
            var cellDisplayRect = dgv_PlayList.GetCellDisplayRectangle(cell.ColumnIndex, cell.RowIndex, false);
            toolTip.Show(message, dgv_PlayList,
                cellDisplayRect.X + cell.Size.Width / 2,
                cellDisplayRect.Y + cell.Size.Height / 2,
                2000);
        }

        private void SetInitialDirectory()
        {
            if (dgv_PlayList.SelectedRows.Count > 0)
            {
                openFileDialog.InitialDirectory =
                    Path.GetDirectoryName(Playlist[dgv_PlayList.SelectedRows[0].Index].FilePath);
            }
        }

        #endregion

        #region PlayerControl Events

        private void PlaylistFormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason != CloseReason.UserClosing) return;
            e.Cancel = true;
            Hide();
            timer.Enabled = false;
        }

        private void PlayerStateChanged(object sender, PlayerStateEventArgs e)
        {
            if (String.IsNullOrEmpty(Media.FilePath)) return;
            if (!File.Exists(Media.FilePath))
            {
                m_CurrentPlayIndex = -1;
                Text = "Playlist";
                RefreshPlaylist();
                return;
            }

            if (CurrentItem == null) return;
            Text = Player.State + " - " + CurrentItem.FilePath;

            HandleContextMenu();

            if (m_CurrentPlayIndex == -1) return;
            dgv_PlayList.InvalidateRow(m_CurrentPlayIndex);
        }

        private void PlaybackCompleted(object sender, EventArgs e)
        {
            if (Player.State == PlayerState.Closed) return;
            if (Media.Position == Media.Duration)
            {
                CurrentItem.PlayCount++;

                if (AfterPlaybackAction == AfterPlaybackSettingsAction.RemoveFile)
                {
                    RemoveFile(m_CurrentPlayIndex);
                    PlayNext(false);
                    return;
                }

                PlayNext();
            }
        }

        private void FrameDecoded(object sender, FrameEventArgs e)
        {
            if (Media.FilePath != string.Empty && Media.Chapters.Count != 0 && CurrentItem != null &&
                CurrentItem.HasChapter)
            {
                m_PreviousChapterPosition =
                    GetChapters()
                        .Aggregate(
                            (prev, next) => e.SampleTime >= prev.Position && e.SampleTime <= next.Position ? prev : next)
                        .Position;
            }
        }

        private void FramePresented(object sender, FrameEventArgs e)
        {
            if (Media.FilePath != string.Empty && Media.Chapters.Count != 0 && CurrentItem != null &&
                CurrentItem.HasChapter)
            {
                if (e.SampleTime >= m_PreviousChapterPosition)
                {
                    int currentChapterIndex = GetChapterIndexByPosition(m_PreviousChapterPosition);

                    if (CurrentItem.SkipChapters.Contains(currentChapterIndex) &&
                        currentChapterIndex < Media.Chapters.Count) SelectChapter(currentChapterIndex);
                    if (currentChapterIndex == CurrentItem.EndChapter) PlayNext();
                }
            }
        }

        private void EnteringFullScreenMode(object sender, EventArgs e)
        {
            m_WasShowing = Visible;
            Hide();
        }

        private void ExitedFullScreenMode(object sender, EventArgs e)
        {
            if (m_WasShowing) Show(Gui.VideoBox);
        }

        #endregion

        #region Playlist Datagridview Events

        private void dgv_PlayList_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if ((e.State & DataGridViewElementStates.Selected) == DataGridViewElementStates.Selected &&
                m_SelectionColor != Color.Empty)
            {
                var b = new SolidBrush(m_SelectionColor);
                e.Graphics.FillRectangle(b, e.RowBounds);
            }

            bool paintPlayRow = CurrentItem != null && e.RowIndex > -1 && e.RowIndex == m_CurrentPlayIndex;
            if (!paintPlayRow) return;

            var brush = new SolidBrush(m_PlayColor);

            Bitmap icon;

            switch (Player.State)
            {
                case PlayerState.Playing:
                    icon = (Bitmap)PlayButton.BackgroundImage;
                    break;
                case PlayerState.Paused:
                    icon = (Bitmap)PauseButton.BackgroundImage;
                    break;
                case PlayerState.Stopped:
                    icon = (Bitmap)StopButton.BackgroundImage;
                    break;
                default:
                    icon = new Bitmap(24, 24);
                    break;
            }

            var offset = new Point(e.RowBounds.X, e.RowBounds.Y + 2);
            var rect = new Rectangle(e.RowBounds.X + 12, e.RowBounds.Y + 4, e.RowBounds.Width, e.RowBounds.Height - 9);
            e.PaintCellsBackground(e.RowBounds, true);
            e.Graphics.FillRectangle(brush, rect);
            e.Graphics.DrawImage(icon, new Rectangle(offset, new Size(24, 24)), 0, 0, 24, 24, GraphicsUnit.Pixel);
            e.PaintCellsContent(e.RowBounds);
            e.Handled = true;
        }

        private void dgv_PlayList_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            var skipChapterCell = dgv_PlayList.Rows[e.RowIndex].Cells[m_SkipCellIndex];
            var endChapterCell = dgv_PlayList.Rows[e.RowIndex].Cells[m_EndCellIndex];

            if (skipChapterCell.IsInEditMode || endChapterCell.IsInEditMode) e.CellStyle.ForeColor = m_FontColor;
        }

        private void dgv_PlayList_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex == -1)
            {
                if (m_ColumnHeaderTransparency)
                {
                    var brush = new SolidBrush(m_ColumnHeaderColor);
                    e.Graphics.FillRectangle(brush, e.CellBounds);
                }
                else e.PaintBackground(e.CellBounds, true);
                ControlPaint.DrawBorder(e.Graphics, e.CellBounds, m_ColumnHeaderBorderColor, 1,
                    ButtonBorderStyle.Solid, m_ColumnHeaderBorderColor, 0, ButtonBorderStyle.None,
                    m_ColumnHeaderBorderColor, 1, ButtonBorderStyle.Solid, m_ColumnHeaderBorderColor, 1,
                    ButtonBorderStyle.Solid);
                e.PaintContent(e.CellBounds);
                e.Handled = true;
            }

            if (e.RowIndex >= 0)
            {
                if (m_DropShadow && e.FormattedValue != null)
                {
                    string text = e.FormattedValue.ToString();
                    var rect = new Rectangle(e.CellBounds.Location, e.CellBounds.Size);
                    var flags = TextFormatFlags.VerticalCenter |
                                TextFormatFlags.Left |
                                TextFormatFlags.EndEllipsis;

                    TextRenderer.DrawText(e.Graphics, text, e.CellStyle.Font,
                        new Rectangle(new Point(rect.X + 1, rect.Y + 1), rect.Size), m_FontDropShadowColor, flags);

                    e.Paint(e.CellBounds, DataGridViewPaintParts.ContentForeground);
                }
            }
        }

        private void dgv_PlayList_RowsAdded(object sender, DataGridViewRowsAddedEventArgs e)
        {
            SetControlStates();
        }

        private void dgv_PlayList_RowsRemoved(object sender, DataGridViewRowsRemovedEventArgs e)
        {
            SetControlStates();
        }

        private void dgv_PlayList_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            PlaySelectedFile();
        }

        private void dgv_PlayList_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            ParseChapterInput();
        }

        private void dgv_PlayList_ColumnStateChanged(object sender, DataGridViewColumnStateChangedEventArgs e)
        {
            if (e.Column.Name == "Duration" && e.Column.Visible)
            {
                Task.Factory.StartNew(GetMediaDuration);
                if (CurrentItem != null) Task.Factory.StartNew(GetCurrentMediaDuration);
            }
        }

        private void dgv_PlayList_SelectionChanged(object sender, EventArgs e)
        {
            if (Playlist.Count == 0) return;
            SetInitialDirectory();
        }

        private void dgv_PlayList_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            e.Control.KeyPress -= dgv_PlayList_HandleInput;
            if (dgv_PlayList.CurrentCell.ColumnIndex <= 1) return;

            var tb = e.Control as TextBox;
            if (tb != null) tb.KeyPress += dgv_PlayList_HandleInput;
        }

        private void dgv_PlayList_CellMouseEnter(object sender, DataGridViewCellEventArgs e)
        {
            if (!ShowToolTips) return;
            if (dgv_PlayList.Rows == null && dgv_PlayList.Rows.Count == 0) return;
            int row = e.RowIndex;
            if (row == -1) return;
            var item = Playlist[row];
            if (item == null) return;

            m_PlayCountToolTip = new ToolTip
            {
                InitialDelay = 475
            };

            row++;
            if (item.PlayCount < 4)
            {
                m_PlayCountToolTip.SetToolTip(dgv_PlayList,
                    "[" + row + "] Played " + item.PlayCount + (item.PlayCount == 1 ? " time!" : " times!"));
            }
            else
            {
                m_PlayCountToolTip.SetToolTip(dgv_PlayList,
                    "[" + row + "] Played " + item.PlayCount + " times!\nHow many times are you going to play this?");
            }
        }

        private void dgv_PlayList_CellMouseLeave(object sender, DataGridViewCellEventArgs e)
        {
            if (!ShowToolTips) return;
            if (m_PlayCountToolTip != null) m_PlayCountToolTip.Dispose();
        }

        private void dgv_PlayList_HandleInput(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && e.KeyChar != ',' && e.KeyChar != ' ' &&
                dgv_PlayList.CurrentCell.ColumnIndex == m_SkipCellIndex)
            {
                ShowCurrentCellTooltip("Only numbers are allowed. You may separate them with a comma or a space.");
                e.Handled = true;
            }

            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) &&
                dgv_PlayList.CurrentCell.ColumnIndex == m_EndCellIndex)
            {
                ShowCurrentCellTooltip("Only numbers are allowed.");
                e.Handled = true;
            }
        }

        private void dgv_Playlist_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Tab)
            {
                SelectNextEditableCell();
                e.SuppressKeyPress = true;
                e.Handled = true;
            }

            if (e.KeyCode == Keys.Delete) RemoveSelectedItems();

            if (e.KeyCode == Keys.Enter)
            {
                PlaySelectedFile();
                e.Handled = true;
            }
        }

        private void dgv_PlayList_MouseMove(object sender, MouseEventArgs e)
        {
            if (Playlist.Count < 2) return;
            if (e.Button != MouseButtons.Left) return;
            if (m_DragRowRect != Rectangle.Empty && !m_DragRowRect.Contains(e.X, e.Y) && m_IsDragging) dgv_PlayList.DoDragDrop(dgv_PlayList.Rows[m_DragRowIndex], DragDropEffects.Move);
        }

        private void dgv_PlayList_MouseDown(object sender, MouseEventArgs e)
        {
            var hit = dgv_PlayList.HitTest(e.X, e.Y);
            m_DragRowIndex = dgv_PlayList.HitTest(e.X, e.Y).RowIndex;

            if (m_DragRowIndex != -1)
            {
                m_IsDragging = true;
                var dragSize = SystemInformation.DragSize;
                m_DragRowRect = new Rectangle(new Point(e.X - (dragSize.Width / 2), e.Y - (dragSize.Height / 2)), dragSize);
            }
            else m_DragRowRect = Rectangle.Empty;

            if (e.Button == MouseButtons.Right)
            {
                if (hit.Type == DataGridViewHitTestType.ColumnHeader)
                {
                    SetColumnStates();
                    dgv_PlaylistColumnContextMenu.Show(Cursor.Position);
                }
                else
                {
                    HandleContextMenu();
                    dgv_PlaylistContextMenu.Show(Cursor.Position);
                }
            }
        }

        private void dgv_PlayList_MouseUp(object sender, MouseEventArgs e)
        {
            m_DragRowIndex = dgv_PlayList.HitTest(e.X, e.Y).RowIndex;

            if (m_DragRowIndex != -1) m_IsDragging = false;
        }

        private void dgv_PlayList_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }

        private void dgv_PlayList_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length == 1)
                {
                    string filename = files[0];

                    if (Directory.Exists(filename))
                    {
                        var media = m_PlayListUi.GetAllMediaFiles(filename);
                        AddFiles(media.ToArray());
                        return;
                    }
                    if (PlayerExtensions.Playlist.Playlist.IsPlaylistFile(filename))
                    {
                        OpenPlaylist(filename);
                        return;
                    }
                }

                var mediaFiles = new List<string>();

                foreach (string p in files)
                {
                    var attr = File.GetAttributes(p);
                    bool isFolder = (attr & FileAttributes.Directory) == FileAttributes.Directory;

                    if (!isFolder) continue;
                    if (Directory.Exists(p)) mediaFiles.AddRange(m_PlayListUi.GetAllMediaFiles(p));
                }

                if (mediaFiles.Count > 0) AddFiles(mediaFiles.NaturalSort().ToArray());

                var actualFiles =
                    files.Where(file => !Directory.Exists(file))
                        .Where(f => PathHelper.GetExtension(f).Length > 0)
                        .Where(file => openFileDialog.Filter.Contains(Path.GetExtension(file.ToLower())))
                        .OrderBy(f => f, new NaturalSortComparer()).ToList();
                AddFiles(actualFiles.NaturalSort().ToArray());

                dgv_PlayList.CurrentCell = dgv_PlayList.Rows[dgv_PlayList.Rows.Count - 1].Cells[m_TitleCellIndex];
                SetPlayStyling();
            }
            else if (e.Data.GetDataPresent(typeof(DataGridViewRow)))
            {
                var clientPoint = dgv_PlayList.PointToClient(new Point(e.X, e.Y));
                int destinationRow = dgv_PlayList.HitTest(clientPoint.X, clientPoint.Y).RowIndex;

                if (destinationRow == -1 || destinationRow >= Playlist.Count) return;
                var playItem = Playlist.ElementAt(m_DragRowIndex);
                Playlist.RemoveAt(m_DragRowIndex);
                NotifyPlaylistChanged();
                Playlist.Insert(destinationRow, playItem);
                PopulatePlaylist();
                dgv_PlayList.CurrentCell = dgv_PlayList.Rows[destinationRow].Cells[m_TitleCellIndex];
            }
        }

        #endregion

        #region Button Events

        private void ButtonPlayClick(object sender, EventArgs e)
        {
            if (dgv_PlayList.CurrentCell.RowIndex != m_CurrentPlayIndex) PlaySelectedFile();
            else
            {
                switch (Player.State)
                {
                    case PlayerState.Paused:
                        Media.Play();
                        break;
                    case PlayerState.Playing:
                        Media.Pause();
                        break;
                    default:
                        PlaySelectedFile();
                        break;
                }
            }
        }

        private void ButtonAddFilesClick(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog(this) != DialogResult.OK) return;

            var fileNames = openFileDialog.FileNames;

            AddFiles(fileNames);
            dgv_PlayList.Focus();
        }

        private void ButtonAddFolderClick(object sender, EventArgs e)
        {
            AddFolderToPlaylist();
            dgv_PlayList.Focus();
        }

        private void ButtonAddFromClipboardClick(object sender, EventArgs e)
        {
            AddClipboardToPlaylist();
            dgv_PlayList.Focus();
        }

        private void ButtonOpenFilesClick(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog(this) != DialogResult.OK) return;

            ClearPlaylist();

            var fileNames = openFileDialog.FileNames;

            OpenFiles(fileNames);
            dgv_PlayList.Focus();
        }

        private void ButtonOpenFolderClick(object sender, EventArgs e)
        {
            OpenFolder();
            dgv_PlayList.Focus();
        }

        private void ButtonOpenFromClipboardClick(object sender, EventArgs e)
        {
            OpenClipboard();
            dgv_PlayList.Focus();
        }

        private void ButtonRemoveSelectedItemsClick(object sender, EventArgs e)
        {
            RemoveSelectedItems();
            dgv_PlayList.Focus();
        }

        private void ButtonRemoveUnselectedItemsClick(object sender, EventArgs e)
        {
            RemoveUnselectedItems();
            dgv_PlayList.Focus();
        }

        private void ButtonRemoveNonExistentItemsClick(object sender, EventArgs e)
        {
            RemoveNonExistentItems();
            dgv_PlayList.Focus();
        }

        private void ButtonNewPlaylistClick(object sender, EventArgs e)
        {
            NewPlaylist(true);
        }

        private void ButtonAddPlaylistClick(object sender, EventArgs e)
        {
            OpenPlaylist(false);
        }

        private void ButtonOpenPlaylistClick(object sender, EventArgs e)
        {
            OpenPlaylist();
        }

        private void ButtonSavePlaylistClick(object sender, EventArgs e)
        {
            SavePlaylist();
        }

        private void ButtonSavePlaylistAsClick(object sender, EventArgs e)
        {
            SavePlaylistAs();
        }

        private void ButtonLeftClick(object sender, EventArgs e)
        {
            PlayPrevious();
        }

        private void ButtonRightClick(object sender, EventArgs e)
        {
            PlayNext();
        }

        private void ButtonViewFileLocation(object sender, EventArgs e)
        {
            ViewFileLocation();
        }

        private void ButtonViewMediaInfo(object sender, EventArgs e)
        {
            ViewMediaInfo();
        }

        private void ButtonSortAscendingClick(object sender, EventArgs e)
        {
            SortPlayList();
        }

        private void ButtonSortDescendingClick(object sender, EventArgs e)
        {
            SortPlayList(false);
        }

        private void ButtonShuffleClick(object sender, EventArgs e)
        {
            ShufflePlayList();
        }

        private void ButtonRepeatPlaylistClick(object sender, EventArgs e)
        {
            if (m_PlayListUi.Settings.AfterPlaybackOpt != AfterPlaybackSettingsOpt.RepeatPlaylist)
                prevAfterPlaybackOpt = m_PlayListUi.Settings.AfterPlaybackOpt;

            m_PlayListUi.Settings.AfterPlaybackOpt = m_PlayListUi.Settings.AfterPlaybackOpt != AfterPlaybackSettingsOpt.RepeatPlaylist ? AfterPlaybackSettingsOpt.RepeatPlaylist : prevAfterPlaybackOpt;
            SetControlStates();
        }

        private void ButtonRestoreClick(object sender, EventArgs e)
        {
            RestoreRememberedPlaylist();
        }

        private void ButtonSettingsClick(object sender, EventArgs e)
        {
            m_PlayListUi.ShowConfigDialog(this);
            m_PlayListUi.Reinitialize();
        }

        #endregion

        #region Form Events

        private void PlaylistForm_Load(object sender, EventArgs e)
        {
            SetColumnSize();
        }

        private void PlaylistForm_Shown(object sender, EventArgs e)
        {
            SetColumnSize();
            FitColumnsToHeader();
        }

        private void PlaylistForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.Escape) Hide();

            if (e.KeyCode == Keys.V && e.Modifiers == (Keys.Control)) AddClipboardToPlaylist();

            if (e.KeyCode == Keys.P && e.Modifiers == (Keys.Control | Keys.Alt))
            {
                m_PlayListUi.ViewPlaylist();
                e.SuppressKeyPress = true;
                e.Handled = true;
            }

            if (e.KeyCode == Keys.O)
            {
                m_PlayListUi.ShowConfigDialog(this);
                m_PlayListUi.Reinitialize();
            }

            if (e.KeyCode == Keys.Tab && e.Modifiers == Keys.Control)
            {
                var form = Player.ActiveForm;
                if (Player.FullScreenMode.Active || form.ContainsFocus) return;
                form.Activate();
                Cursor.Position = new Point(form.Location.X + 100, form.Location.Y + 100);
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
        }

        #endregion

        #region Threaded Methods

        public void GetCurrentMediaDuration()
        {
            try
            {
                var time = TimeSpan.FromMilliseconds(Media.Duration / 1000.0);
                CurrentItem.Duration = time.ToString(@"hh\:mm\:ss");
                GuiThread.DoAsync(() =>
                {
                    if (dgv_PlayList.Rows.Count < 1) return;
                    dgv_PlayList.Rows[m_CurrentPlayIndex].Cells["Duration"].Value = time.ToString(@"hh\:mm\:ss");
                    dgv_PlayList.InvalidateRow(m_CurrentPlayIndex);
                });
            }
            catch (Exception ex)
            {
                GuiThread.DoAsync(() => Player.HandleException(ex));
            }
        }

        public void GetMediaDuration()
        {
            try
            {
                for (var i = 0; i < Playlist.Count; i++)
                {
                    var item = Playlist[i];
                    if (!String.IsNullOrEmpty(item.Duration)) continue;
                    var media = new MediaFile(item.FilePath);
                    var time = TimeSpan.FromMilliseconds(media.duration);
                    item.Duration = time.ToString(@"hh\:mm\:ss");

                    int idx = i;
                    GuiThread.DoAsync(() =>
                    {
                        if (dgv_PlayList.Rows.Count < 1) return;
                        if (idx != m_CurrentPlayIndex || !string.IsNullOrEmpty(item.Duration))
                        {
                            dgv_PlayList.Rows[idx].Cells["Duration"].Value = time.ToString(@"hh\:mm\:ss");
                            dgv_PlayList.InvalidateRow(idx);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                GuiThread.DoAsync(() => Player.HandleException(ex));
            }
        }

        #endregion

        #region Timer Stuff

        private void HandleOpacity()
        {
            var pos = MousePosition;
            bool inForm = pos.X >= Left && pos.Y >= Top && pos.X < Right && pos.Y < Bottom;

            if (inForm || ActiveForm == this)
            {
                if (Opacity < MAX_OPACITY) Opacity += 0.1;
            }
            else if (Opacity > MIN_OPACITY) Opacity -= 0.1;
        }

        private void TimerTick(object sender, EventArgs e)
        {
            HandleOpacity();
        }

        #endregion
    }

    #region PlaylistItem

    public class PlaylistItem
    {
        public string FilePath { get; set; }
        public bool Active { get; set; }
        public bool HasChapter { get; set; }
        public List<int> SkipChapters { get; set; }
        public int EndChapter { get; set; }
        public string Duration { get; set; }
        public int PlayCount { get; set; }

        public PlaylistItem(string filePath, bool isActive)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException("filePath");

            FilePath = filePath;
            Active = isActive;
            PlayCount = 0;
        }

        public PlaylistItem(string filePath, List<int> skipChapter, int endChapter, bool isActive)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException("filePath");

            FilePath = filePath;
            Active = isActive;
            SkipChapters = skipChapter;
            EndChapter = endChapter;
            HasChapter = true;
            PlayCount = 0;
        }

        public PlaylistItem(string filePath, List<int> skipChapter, int endChapter, bool isActive, string duration)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException("filePath");

            FilePath = filePath;
            Active = isActive;
            SkipChapters = skipChapter;
            EndChapter = endChapter;
            HasChapter = true;
            Duration = duration;
            PlayCount = 0;
        }

        public PlaylistItem(string filePath, List<int> skipChapter, int endChapter, bool isActive, string duration,
            int playCount)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException("filePath");

            FilePath = filePath;
            Active = isActive;
            SkipChapters = skipChapter;
            EndChapter = endChapter;
            HasChapter = true;
            Duration = duration;
            PlayCount = playCount;
        }

        public override string ToString()
        {
            if (HasChapter)
            {
                return Path.GetFileName(FilePath) + " | SkipChapter: " + String.Join(",", SkipChapters) +
                       " | EndChapter: " + EndChapter;
            }

            return Path.GetFileName(FilePath) ?? "???";
        }
    }

    #endregion

    #region CustomEventArgs

    public class RegexEventArgs : EventArgs
    {
        public List<string> RegexList { get; internal set; }
        public bool StripDirectoryInFileName { get; internal set; }

        public RegexEventArgs(List<string> regexList, bool stripDirectory)
        {
            RegexList = regexList;
            StripDirectoryInFileName = stripDirectory;
        }
    }

    #endregion

    #region PlaylistDataGrid

    public class PlaylistDataGrid : DataGridView
    {
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000; // WS_CLIPCHILDREN
                return cp;
            }
        }

        public PlaylistDataGrid()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.DoubleBuffer |
                     ControlStyles.Opaque |
                     ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnScroll(ScrollEventArgs e)
        {
            base.OnScroll(e);
            Invalidate();
        }

        protected override void PaintBackground(Graphics graphics, Rectangle clipBounds, Rectangle gridBounds)
        {
            base.PaintBackground(graphics, clipBounds, gridBounds);
            if (BackgroundImage == null) return;
            graphics.DrawImage(BackgroundImage, gridBounds);
        }

        public void ResetCells()
        {
            AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248);
            DefaultCellStyle.BackColor = Color.White;
        }

        public void SetCellsTransparent()
        {
            AlternatingRowsDefaultCellStyle.BackColor = Color.Transparent;
            DefaultCellStyle.BackColor = Color.Transparent;
        }

        public void ResetColumnHeader()
        {
            EnableHeadersVisualStyles = true;
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Raised;
            AutoResizeColumnHeadersHeight();
        }

        public void SetColumnHeaderTransparent()
        {
            EnableHeadersVisualStyles = false;
            ColumnHeadersDefaultCellStyle.BackColor = Color.Transparent;
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            AutoResizeColumnHeadersHeight();
        }
    }

    #endregion

    #region CustomStatusStrip

    public class CustomStatusStrip : StatusStrip
    {
        public Color BorderColor;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var brush = new SolidBrush(BorderColor);
            e.Graphics.FillRectangle(brush, new Rectangle(0, 0, Width, 1));
        }
    }

    #endregion

    #region ToolstripItem Proxy

    [ToolStripItemDesignerAvailability(ToolStripItemDesignerAvailability.StatusStrip)]
    public class ButtonStripItem : ToolStripControlHostProxy
    {
        private bool isHovering;

        public ButtonStripItem()
            : base(CreateButtonInstance())
        {}

        private static Button CreateButtonInstance()
        {
            var b = new Button {BackColor = Color.Transparent, FlatStyle = FlatStyle.Flat};
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.BorderColor = Color.FromArgb(0, 255, 255, 255);
            b.FlatAppearance.MouseDownBackColor = Color.Transparent;
            b.FlatAppearance.MouseOverBackColor = Color.Transparent;
            return b;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            isHovering = true;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            isHovering = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Image == null) return;

            if (!Enabled || (Tag != null && Tag.Equals("Disabled")))
            {
                var greyScaleMatrix = new ColorMatrix(new[]
                {
                    new float[] {0.30f, 0.30f, 0.30f, 0, 0},
                    new float[] {0.59f, 0.59f, 0.59f, 0, 0},
                    new float[] {0.11f, 0.11f, 0.11f, 0, 0},
                    new float[] {0, 0, 0, 1, 0},
                    new float[] {0, 0, 0, 0, 1}
                });

                var attr = new ImageAttributes();
                attr.SetColorMatrix(greyScaleMatrix);
                attr.SetWrapMode(WrapMode.TileFlipXY);

                e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
                e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                e.Graphics.DrawImage(Image,
                    new Rectangle(
                        new Point(Width / 2 - PlaylistForm.IconSize / 2, Height / 2 - PlaylistForm.IconSize / 2),
                        new Size(PlaylistForm.IconSize, PlaylistForm.IconSize)), 0, 0, Image.Width,
                    Image.Height, GraphicsUnit.Pixel, attr);
            }
            else
            {
                if (!isHovering)
                {
                    var attr = new ImageAttributes();
                    attr.SetWrapMode(WrapMode.TileFlipXY);

                    e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
                    e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
                    e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                    e.Graphics.DrawImage(Image,
                        new Rectangle(
                            new Point(Width / 2 - PlaylistForm.IconSize / 2, Height / 2 - PlaylistForm.IconSize / 2),
                            new Size(PlaylistForm.IconSize, PlaylistForm.IconSize)), 0, 0, Image.Width,
                        Image.Height, GraphicsUnit.Pixel, attr);
                }
                else
                {
                    var attr = new ImageAttributes();
                    attr.SetWrapMode(WrapMode.TileFlipXY);

                    e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
                    e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
                    e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                    var brush = new SolidBrush(PlaylistForm.StatusHighlightColor);
                    e.Graphics.FillRectangle(brush,
                        new Rectangle(Point.Empty, new Size(Width, Height)));

                    e.Graphics.DrawImage(Image,
                        new Rectangle(
                            new Point(Width / 2 - PlaylistForm.IconSize / 2, Height / 2 - PlaylistForm.IconSize / 2),
                            new Size(PlaylistForm.IconSize, PlaylistForm.IconSize)), 0, 0, Image.Width,
                        Image.Height, GraphicsUnit.Pixel, attr);
                }
            }
        }
    }

    public class ToolStripControlHostProxy : ToolStripControlHost
    {
        public ToolStripControlHostProxy()
            : base(new Control()) {}

        public ToolStripControlHostProxy(Control c)
            : base(c) {}
    }

    #endregion

    #region Form base

    public class FormEx : Form
    {
        private float m_ScaleFactorHeight = -1f;
        private float m_ScaleFactorWidth = -1f;
        protected SizeF ScaleFactor { get; private set; }

        protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
        {
            base.ScaleControl(factor, specified);

            if (!(m_ScaleFactorWidth < 0 || m_ScaleFactorHeight < 0)) return;

            if (m_ScaleFactorWidth < 0 && specified.HasFlag(BoundsSpecified.Width)) m_ScaleFactorWidth = factor.Width;
            if (m_ScaleFactorHeight < 0 && specified.HasFlag(BoundsSpecified.Height)) m_ScaleFactorHeight = factor.Height;

            if (m_ScaleFactorWidth < 0 || m_ScaleFactorHeight < 0) return;

            ScaleFactor = new SizeF(m_ScaleFactorWidth, m_ScaleFactorHeight);
        }
    }

    #endregion

    #region Natural Sorting

    [SuppressUnmanagedCodeSecurity]
    internal static class SafeNativeMethods
    {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        public static extern int StrCmpLogicalW(string psz1, string psz2);
    }

    public class NaturalSortComparer : IComparer<string>
    {
        public NaturalSortComparer() : this(false) {}
        public NaturalSortComparer(bool descending) {}

        public int Compare(string a, string b)
        {
            var arrayA = a.Split(Path.DirectorySeparatorChar);
            var arrayB = b.Split(Path.DirectorySeparatorChar);

            int length = Math.Max(arrayA.Length, arrayB.Length);

            for (var i = 0; i < length; i++)
            {
                int result = SafeNativeMethods.StrCmpLogicalW(arrayA.Length > i ? arrayA[i].ToLower() : string.Empty,
                    arrayB.Length > i ? arrayB[i].ToLower() : string.Empty);

                if (result != 0) return result;
            }

            return 0;
        }
    }

    public static class ThreadSafeRandom
    {
        [ThreadStatic] private static Random Local;

        public static Random ThisThreadsRandom
        {
            get
            {
                return Local ??
                       (Local = new Random(unchecked(Environment.TickCount * 31 + Thread.CurrentThread.ManagedThreadId)));
            }
        }
    }

    #endregion

    #region Extensions Methods

    public static class ListExtensions
    {
        public static IList<string> NaturalSort(this IList<string> list)
        {
            return list.OrderBy(f => Path.GetDirectoryName(f), new NaturalSortComparer())
                .ThenBy(f => Path.GetFileName(f), new NaturalSortComparer()).ToList();
        }

        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = ThreadSafeRandom.ThisThreadsRandom.Next(n + 1);
                var value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }

    #endregion
}
