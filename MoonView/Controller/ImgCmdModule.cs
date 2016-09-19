﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MoonView.Forms;
using System.Windows.Forms;
using System.IO;
using MoonView.FileSystem;

namespace MoonView.Controller
{
    class ImgCmdModule
    {
        CompareForm comparer = new CompareForm();

        public void InitCmd(string tag, IEnumerable<string> files)
        {
            try
            {
                MessageCache.Instance().Add(DateTime.Now.ToString());
                MessageCache.Instance().Add("Command Init: " + tag);

                if (String.IsNullOrEmpty(Utility.Config.AnchorSource))
                    MessageBox.Show("Source anchor is not fixed");
                else if (String.IsNullOrEmpty(Utility.Config.AnchorDest))
                    MessageBox.Show("Destination is not fixed");
                else
                {
                    string action1 = tag.Split(new String[] { "::" }, StringSplitOptions.RemoveEmptyEntries)[0];
                    string action2 = tag.Split(new String[] { "::" }, StringSplitOptions.RemoveEmptyEntries)[1];
                    string destDir = tag.Split(new String[] { "::" }, StringSplitOptions.RemoveEmptyEntries)[2];

                    PerformAction(files, destDir, action1, action2);

                    MessageCache.Instance().Add("Process completed at " + DateTime.Now.ToString());
                    MessageCache.Instance().Add("  ");
                    MessageCache.Instance().Show("Done :: " + tag);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.StackTrace);
            }
        }

        protected void PerformAction(IEnumerable<string> files, string dest, string action1, string action2)
        {
            try
            {
                if (action1 == "NEW")
                {
                    string folderName = Prompt.ShowDialog("Directory Name:", "New Directory");
                    dest = System.IO.Path.Combine(dest, folderName);
                    Directory.CreateDirectory(dest);
                    MessageCache.Instance().Add("Folder created: " + dest);
                }

                if (action2 == "NOPE")
                    return;

                MessageCache.Instance().Add("Processing Files...");
                MessageCache.Instance().Add("--------------------");

                //  Builder for the output.
                var builder = new StringBuilder();
                int count = 0;

                //  Go through each file.
                foreach (var filePath in files)
                {
                    // donot include blog directory
                    //string tail = filePath.Replace(config.Set["anchor"].ToString(), "");
                    //tail = tail.Replace(Enclose(tail, @"\", @"\"), "");
                    //string newPath = dest + @"\" + tail;


                    //include blog directory
                    string newPath = dest + @"\" + filePath.Replace(Utility.Config.AnchorSource, "");

                    if (File.Exists(newPath))
                    {
                        string uniquePath = Utils.GetUniqueFileName(newPath);
                        comparer.Show(filePath, newPath, uniquePath);
                        string choice = comparer.Choice;
                        switch (choice)
                        {
                            case "skip":
                                MessageCache.Instance().Add(filePath + " skipped");
                                break;

                            case "write":
                                DoAction(filePath, newPath, action2);
                                //MessageCache.Instance().Add(filePath + " overwrite");
                                //count++;
                                break;

                            case "rename":
                                DoAction(filePath, uniquePath, action2);
                                //MessageCache.Instance().Add(filePath + " rename");
                                count++;
                                break;

                        }
                    }
                    else
                    {
                        DoAction(filePath, newPath, action2);
                        //MessageCache.Instance().Add(filePath + " write");
                        count++;
                    }

                    //MessageCache.Instance().Add(String.Format("{0}: {1} > {2}", action2, filePath, newPath));


                }

                Utility.Config.RecentList.Add(dest);
                //RecentStack.Add(new LastItem("", dest));
                //RecentStack.Save();

                MessageCache.Instance().Add(String.Format("{0} >>> {1} file(s) to {2}", action2, count, dest));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.StackTrace);
            }
        }

        protected void DoAction(string source, string dest, string action)
        {
            Utils.EnsureFilePath(dest);
            string status = string.Empty;
            switch (action)
            {
                case "COPY":
                    File.Copy(source, dest, true);

                    if (File.Exists(dest))
                    {
                        status = "OK";
                    }
                    break;

                case "MOVE":
                    File.Copy(source, dest, true);

                    if (File.Exists(dest))
                    {
                        File.Delete(source);
                        status = "OK";
                    }

                    break;
            }
            MessageCache.Instance().Add(String.Format("{0}: {1} > {2} [{3}]", action, source, dest, status));
        }

        protected string Enclose(string str, string from, string to)
        {

            int pFrom = str.IndexOf(from);
            int pTo = str.IndexOf(to, pFrom + from.Length);

            String result = str.Substring(pFrom, pTo - pFrom);

            return result;
        }
    }
}
