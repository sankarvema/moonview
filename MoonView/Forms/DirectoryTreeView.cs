//System NS
using System;
using System.IO;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;

//MoonView NS
using MoonView.Path;

namespace MoonView.Forms
{
    public class DirectoryTreeView : TreeView
    {
        //Constants
        const int DISK_ICON = 0;
        const int FOLDER_ICON = 1;
        const int ARCH_ICON = 1;

        //Object
        MoonViewForm _parent;

        public new MoonViewForm Parent
        {
            get { return _parent; }
            set { _parent = value; }
        }

        public DirectoryTreeView()
            : base()
        {
            //this.AfterSelect += new TreeViewEventHandler(DirectoryTreeView_AfterSelect);
            //this.KeyDown += new KeyEventHandler(DirectoryTreeView_KeyDown);
            this.DoubleClick += new EventHandler(DirectoryTreeView_DoubleClick);
            this.NodeMouseClick += new TreeNodeMouseClickEventHandler(DirectoryTreeView_NodeMouseClick);
        }

        void DirectoryTreeView_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            
            if (e.Button == MouseButtons.Right)
            {
                string clickedNode = e.Node.Name;
                ContextMenu mnu = InitContextMenu();
                mnu.Show(this,e.Location);
            }

        }

        public void Initialise(MoonViewForm parent)
        {
            _parent = parent;

            this.ImageList = new ImageList();
            this.ImageList.ColorDepth = ColorDepth.Depth32Bit;
            this.ImageList.Images.Add(Utility.DiskIconSmall);
            this.ImageList.Images.Add(Utility.FolderIconSmall);

            PopulateBaseNode();
        }

        void DirectoryTreeView_DoubleClick(object sender, EventArgs e)
        {
            if (this.SelectedNode == null)
                 return;
             string path = "";
             TreeNode currNode = this.SelectedNode;
             while (currNode != null)
             {
                 path = System.IO.Path.Combine(currNode.Name, path);
                 currNode = currNode.Parent;
             }
             _parent.Open(this, path);
        }

        void PopulateBaseNode()
        {
            foreach (string drive in Directory.GetLogicalDrives())
                this.Nodes.Add(drive, drive, DISK_ICON, DISK_ICON);
        }

        public void PopulateNodes(IDirectoryInfo dirInfo)
        {
            List<IDirectoryInfo> pathList = new List<IDirectoryInfo>();
            IDirectoryInfo currDirInfo = dirInfo;
            while (true)
            {
                pathList.Insert(0, currDirInfo);
                Console.WriteLine(currDirInfo.Name);
                currDirInfo = currDirInfo.Parent;
                if (currDirInfo == null)
                    break;
            }

            int i = 0;
            TreeNode lastNode = ExpandNode(ref i, this.Nodes, pathList.ToArray());
            this.Update();
        }

        public TreeNode ExpandNode(ref int level, TreeNodeCollection nodes, IDirectoryInfo[] pathList)
        {
            TreeNode currNode;
            IDirectoryInfo currDirInfo = pathList[level];
            string name = currDirInfo.Name;
            if (nodes.ContainsKey(name))
                currNode = nodes[name];
            else
            {
                currNode = new TreeNode(name, FOLDER_ICON, FOLDER_ICON);
                currNode.Name = name;
                currNode.Tag = currDirInfo.FullPath;
                nodes.Add(currNode);
            }
            level++;
            if (!currNode.IsExpanded)
                currNode.Toggle();
            if (level >= pathList.Length)
                return currNode;
            return ExpandNode(ref level, currNode.Nodes, pathList);
        }

        #region Context Menu Methods
        private ContextMenu InitContextMenu()
        {
            ContextMenu mnu = new System.Windows.Forms.ContextMenu();
            MenuItem miSource = new MenuItem("Source", new EventHandler(MenuClick));
            miSource.Tag = "SRC";
            mnu.MenuItems.Add(miSource);

            MenuItem miDest = new MenuItem("Destination", new EventHandler(MenuClick));
            miDest.Tag = "DEST";
            mnu.MenuItems.Add(miDest);

            mnu.MenuItems.Add(new MenuItem("-"));

            MenuItem miProperties = new MenuItem("Properties", new EventHandler(MenuClick));
            miProperties.Tag = "PROP";
            mnu.MenuItems.Add(miProperties);
            return mnu;

        }

        private void MenuClick(object sender, EventArgs args)
        {
            if (this.SelectedNode.Tag == null)
            {
                MessageBox.Show("Tree node not selected properly", "Alert!!!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            MenuItem mi = (MenuItem)sender;
            MessageBox.Show(mi.Tag + ":" + this.SelectedNode.Tag);

            switch (mi.Tag.ToString())
            { 
                case "SRC":
                    Utility.Config.AnchorSource = this.SelectedNode.Tag.ToString();
                    break;

                case "DEST":
                    Utility.Config.AnchorDest = this.SelectedNode.Tag.ToString();
                    break;

                default:
                    // unknown command ignore
                    break;
            }
            
        }
        #endregion
    }
}
