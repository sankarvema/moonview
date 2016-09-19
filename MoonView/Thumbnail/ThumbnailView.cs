//System NS
using System;
using System.Text;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;
using System.ComponentModel;

//MoonView NS
using MoonView.Path;
using System.IO;
using MoonView.Controller;

namespace MoonView.Thumbnail
{
    public class ThumbnailView : ListView
    {
        //Object
        MoonViewForm _parent;
        //ClearListWorker _clearListWorker;
        LoadContentWorker _loadContentWorker;
        LoadImageWorker _loadImageWorker;
        IDirectoryInfo _currDirInfo;
        ThumbnailWorkerState _thumbnailWorkerState;
        ThumbnailItemComparer _thumbnailSorter = new ThumbnailItemComparer();

        //Flags
        bool _loading = false;

        //Threading
        ManualResetEvent _loadMRE = new ManualResetEvent(true);
        System.Windows.Forms.Timer _showTimer;

        //Collection
        Queue<object[]> _contentQueue = new Queue<object[]>();
        Queue<object[]> _imageQueue = new Queue<object[]>();
        Dictionary<string, string> _lastSelectedItem = new Dictionary<string, string>();
        Dictionary<ListViewItem, IFSInfo> _lvItemDict = new Dictionary<ListViewItem, IFSInfo>();

        public bool IsBusy
        {
            get { return _loading; }
        }

        public IDirectoryInfo CurrentDirectoryInfo
        {
            get { return _currDirInfo; }
        }

        /// <summary>
        /// 
        /// </summary>
        public ThumbnailView()
            : base()
        {
            //_clearListWorker = new ClearListWorker();
            _loadContentWorker = new LoadContentWorker();
            _loadImageWorker = new LoadImageWorker();

            _loadContentWorker.OnCompleted += new LoadContentCompleted(_loadImageWorker.Run);
            _loadImageWorker.OnCompleted += new LoadImageCompleted(_loadImageWorker_OnCompleted);

            _thumbnailWorkerState = new ThumbnailWorkerState(this, _lvItemDict);

            //this.View = View.LargeIcon;
            this.View = View.Details;

            //Detail View
            this.Columns.Add(new ColumnHeader());
            this.Columns.Add(new ColumnHeader());
            this.Columns.Add(new ColumnHeader());
            this.Columns.Add(new ColumnHeader());
            this.Columns.Add(new ColumnHeader());
            this.Columns[0].Text = "Name";
            this.Columns[1].Text = "Size";
            this.Columns[2].Text = "Type";
            this.Columns[3].Text = "Date";
            this.Columns[4].Text = "Resolution";

            this.DoubleBuffered = true;
            this.MultiSelect = true;
            this.FullRowSelect = true;
            this.CheckBoxes = false;

            //Large image list
            this.LargeImageList = new ImageList();
            this.LargeImageList.ImageSize = new Size(128, 128);
            this.LargeImageList.ColorDepth = ColorDepth.Depth32Bit;
            //Small image list
            this.SmallImageList = new ImageList();
            this.SmallImageList.ImageSize = new Size(24, 24);
            this.SmallImageList.ColorDepth = ColorDepth.Depth32Bit;

            //ThumbnailView 
            this.Activation = System.Windows.Forms.ItemActivation.TwoClick;

            //Timer
            _showTimer = new System.Windows.Forms.Timer();
            _showTimer.Interval = 150;
            _showTimer.Tick += new EventHandler(_showTimer_Tick);

            //Sorting
            _thumbnailSorter.Column = 2;
            _thumbnailSorter.ColumnDataType = "String";
            _thumbnailSorter.SortOrder = SortOrder.Ascending;

            //this.ListViewItemSorter = _thumbnailSorter; //Enable sorting

            //Events
            //this.ItemActivate += new EventHandler(ThumbnailView_ItemActivate);
            this.SelectedIndexChanged += new EventHandler(ThumbnailView_SelectedIndexChanged);
            this.MouseDoubleClick += new MouseEventHandler(ThumbnailView_MouseDoubleClick);
            this.ColumnClick += new ColumnClickEventHandler(ThumbnailView_ColumnClick);
            this.MouseDown += new MouseEventHandler(thubnailView_MouseDown);
        }

        void ThumbnailView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.SelectedItems.Count == 0)
                return;
            if (!_lastSelectedItem.ContainsKey(_currDirInfo.FullPath))
                _lastSelectedItem.Add(_currDirInfo.FullPath, this.SelectedItems[0].Text);
            else
                _lastSelectedItem[_currDirInfo.FullPath] = this.SelectedItems[0].Text;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parent"></param>
        public void Initialise(MoonViewForm parent)
        {
            _parent = parent;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void ThumbnailView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            //set the properties of the ListViewItemComparer

            //Dictate the column's datatype
            //Column's Index starts with 0
            if (e.Column == 0 || e.Column == 2)
                _thumbnailSorter.ColumnDataType = "String";
            else if (e.Column == 1)
                _thumbnailSorter.ColumnDataType = "Numeric";
            else if (e.Column == 3)
                _thumbnailSorter.ColumnDataType = "DateTime";

            //Toggle the sorting order
            if (_thumbnailSorter.Column == e.Column)
            {
                if (_thumbnailSorter.SortOrder == SortOrder.Ascending)
                    _thumbnailSorter.SortOrder = SortOrder.Descending;
                else
                    _thumbnailSorter.SortOrder = SortOrder.Ascending;
            }

            //set the column to the column that is clicked
            _thumbnailSorter.Column = e.Column;

            //Call the ListView's Sort Method, perform sorting
            Sort();
        }

        public new void Sort()
        {
            this.ListViewItemSorter = _thumbnailSorter;
            base.Sort();
            this.ListViewItemSorter = null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void ThumbnailView_MouseDoubleClick(object sender, EventArgs e)
        {
            if (this.SelectedItems.Count == 0)
                return;
            if (this.SelectedItems[0] == null)
                return;
            if (!_lvItemDict.ContainsKey(this.SelectedItems[0]))
                return; //TODO Add assertion

            IFSInfo fsInfo = _lvItemDict[this.SelectedItems[0]];

            //Directory
            if (fsInfo is IDirectoryInfo)
                _parent.ShowDirectory(this, (IDirectoryInfo)fsInfo);

            //File
            if (fsInfo is IFileInfo)
            {
                IFileInfo fileInfo = (IFileInfo)fsInfo;
                if (Utility.IsSupported(fileInfo.Extension))
                    _parent.ShowImage(fileInfo);
                if (Utility.IsArchive(fileInfo.Extension))
                    _parent.ShowArchive(sender, fileInfo);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dirInfo"></param>
        void LoadDirectory(IDirectoryInfo dirInfo)
        {
            _loading = true;
            _thumbnailWorkerState.Cancel = false;
            _thumbnailWorkerState.DirectoryInfo = dirInfo;

            _loadContentWorker.Run(this, _thumbnailWorkerState);
        }

        /// <summary>
        /// 
        /// </summary>
        void _loadImageWorker_OnCompleted()
        {
            _loading = false;
            if (!_lastSelectedItem.ContainsKey(_currDirInfo.FullPath))
                return;
            foreach (ListViewItem item in this.Items)
            {
                if (item.Text.Equals(_lastSelectedItem[_currDirInfo.FullPath]))
                {
                    this.TopItem = item;
                    item.Selected = true;
                    item.Focused = true;
                    break;
                }
            }
        }

        /// <summary>
        /// Abort background worker loading
        /// </summary>
        public void AbortLoading()
        {
            _thumbnailWorkerState.Cancel = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dirInfo"></param>
        public void ShowDirectory(IDirectoryInfo dirInfo)
        {
            if (_loading)
                this.AbortLoading();
            _currDirInfo = dirInfo;
            _showTimer.Start();
        }

        /// <summary>
        /// Check is background worker completed and run background worker.
        /// If background worker is not completed, check again at next timer tick event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void _showTimer_Tick(object sender, EventArgs e)
        {
            if (_loading)
                return;
            _showTimer.Stop();
            LoadDirectory(_currDirInfo);
        }

        public void SetView(View view)
        {
            this.View = view;
            if (view == View.Details)
                ResizeColumns();
        }

        public void ResizeColumns()
        {
            foreach (ColumnHeader header in this.Columns)
            {
                header.AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
                if (header.Width < 50)
                    header.Width = 50;
            }
        }

        private void thubnailView_MouseDown(object sender, MouseEventArgs e)
        {
            bool match = false;

            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                foreach (ListViewItem item in this.Items)
                {
                    if (item.Bounds.Contains(new Point(e.X, e.Y)))
                    {
                        this.ContextMenu = InitImageContextMenu();
                        match = true;
                        break;
                    }
                }
                if (match)
                {
                    this.ContextMenu.Show(this, new Point(e.X, e.Y));
                }
                else
                {
                    //Show listViews context menu
                }

            }

        }

        #region Context Menu Methods
        private ContextMenu InitImageContextMenu()
        {
            ContextMenu mnu = new System.Windows.Forms.ContextMenu();

            MenuItem miMove = new MenuItem("Move", new EventHandler(MenuClick));
            miMove.Tag = "MOVE";
            mnu.MenuItems.Add(miMove);

            MenuItem miCopy = new MenuItem("Copy", new EventHandler(MenuClick));
            miCopy.Tag = "COPY";
            mnu.MenuItems.Add(miCopy);

            mnu.MenuItems.Add(new MenuItem("-"));

            MenuItem miProperties = new MenuItem("Properties", new EventHandler(MenuClick));
            miProperties.Tag = "PROP";
            mnu.MenuItems.Add(miProperties);

            //BuildRecent(miCopy, miMove);
            BuildMenu(miCopy, miMove, Utility.Config.AnchorDest);

            return mnu;

        }

        //protected void BuildRecent(MenuItem copyMenu, MenuItem moveMenu)
        //{
        //    //RecentStack.Read();
        //    foreach (LastItem item in RecentStack.Items)
        //    {
        //        string dispItem = GetDispItem(item);

        //        MenuItem mnuRecentMove = new MenuItem(dispItem);
        //        mnuRecentMove.Click += MenuSelected;
        //        mnuRecentMove.Tag = "RECENT::MOVE::" + item.path;
        //        moveMenu.DropDownItems.Add(mnuRecentMove); 
                
        //        MenuItem mnuRecentCopy = new MenuItem(dispItem);
        //        mnuRecentCopy.Click += MenuSelected;
        //        mnuRecentCopy.Tag = "RECENT::COPY::" + item.path;
        //        copyMenu.DropDownItems.Add(mnuRecentCopy);
        //    }
        //    ToolStripSeparator seperatorMenu1 = new ToolStripSeparator();
        //    moveMenu.DropDownItems.Add(seperatorMenu1);

        //    ToolStripSeparator seperatorMenu2 = new ToolStripSeparator();
        //    copyMenu.DropDownItems.Add(seperatorMenu2);
        //}

        protected void BuildMenu(MenuItem copyMenu, MenuItem moveMenu, String dir)
        {
            if (moveMenu.MenuItems.Count == 0)
            {
                MenuItem mnuNewFolder = new MenuItem("New");
                mnuNewFolder.Click += MenuClick;
                mnuNewFolder.Tag = "NEW::NOPE::" + dir;
                moveMenu.MenuItems.Add(mnuNewFolder);

                MenuItem mnuAddFolder = new MenuItem("New + Move");
                mnuAddFolder.Click += MenuClick;
                mnuAddFolder.Tag = "NEW::MOVE::" + dir;
                moveMenu.MenuItems.Add(mnuAddFolder);

                MenuItem mnuCopyFile = new MenuItem("Move");
                mnuCopyFile.Click += MenuClick;
                mnuCopyFile.Tag = "NOPE::MOVE::" + dir;
                moveMenu.MenuItems.Add(mnuCopyFile);

                moveMenu.MenuItems.Add(new MenuItem("-"));
            }

            if (copyMenu.MenuItems.Count == 0)
            {
                MenuItem mnuNewFolder = new MenuItem("New");
                mnuNewFolder.Click += MenuClick;
                mnuNewFolder.Tag = "NEW::NOPE::" + dir;
                copyMenu.MenuItems.Add(mnuNewFolder);

                MenuItem mnuAddFolder = new MenuItem("New + Copy");
                mnuAddFolder.Click += MenuClick;
                mnuAddFolder.Tag = "NEW::COPY::" + dir;
                copyMenu.MenuItems.Add(mnuAddFolder);

                MenuItem mnuCopyFile = new MenuItem("Copy");
                mnuCopyFile.Click += MenuClick;
                mnuCopyFile.Tag = "NOPE::COPY::" + dir;
                copyMenu.MenuItems.Add(mnuCopyFile);

                copyMenu.MenuItems.Add(new MenuItem("-"));
            }

            try
            {
                foreach (string d in Directory.GetDirectories(dir))
                {
                    MenuItem mnuMove = new MenuItem(d.Substring(d.LastIndexOf("\\") + 1));
                    mnuMove.Click += MenuClick;
                    mnuMove.Tag = "DIR::" + d;
                    moveMenu.MenuItems.Add(mnuMove);

                    MenuItem mnuCopy = new MenuItem(d.Substring(d.LastIndexOf("\\") + 1));
                    mnuCopy.Click += MenuClick;
                    mnuCopy.Tag = "DIR::" + d;
                    copyMenu.MenuItems.Add(mnuCopy);

                    BuildMenu(mnuCopy, mnuMove, d);
                }
            }
            catch (System.Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private void MenuClick(object sender, EventArgs args)
        {
            if (this.SelectedItems.Count == 0)
            {
                MessageBox.Show("Thumbnails not selected properly", "Alert!!!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            MenuItem mi = (MenuItem)sender;
            MessageBox.Show(mi.Tag + ":" + this.SelectedItems.Count);

            StringBuilder msg = new StringBuilder();
            foreach (ListViewItem item in this.SelectedItems)
            {
                msg.AppendLine(item.Text + ":" + item.Tag);
            }
            MessageBox.Show(msg.ToString());

            ImgCmdModule cmd = new ImgCmdModule();
            List<string> selectedItems = new List<string>();
            foreach (ListViewItem item in this.SelectedItems)
            {
                selectedItems.Add(item.Tag.ToString());
            }

            cmd.InitCmd(mi.Tag.ToString(), selectedItems);

        }
        #endregion
    }

    /// <summary>
    /// 
    /// </summary>
    public class ThumbnailWorkerState
    {
        public ThumbnailView ThumbnailView;
        public bool Cancel;
        public IDirectoryInfo DirectoryInfo;
        public Dictionary<ListViewItem, IFSInfo> ListViewDict;

        public ThumbnailWorkerState(ThumbnailView thumbnailView, Dictionary<ListViewItem, IFSInfo> listViewDict)
        {
            ThumbnailView = thumbnailView;
            Cancel = false;
            DirectoryInfo = null;
            ListViewDict = listViewDict;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public struct ThumbnailMetaItem
    {
        public IFSInfo FsInfo;
        public ListViewItem ListViewItem;
        public Bitmap LargeIcon;
        public Bitmap SmallIcon;

        public ThumbnailMetaItem(IFSInfo fsInfo, ListViewItem lvItem)
        {
            FsInfo = fsInfo;
            ListViewItem = lvItem;
            LargeIcon = null;
            SmallIcon = null;
        }
    }
}
