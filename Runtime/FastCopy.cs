﻿using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Serialization;

namespace Megumin.IO
{
    public static class CopyUtility
    {
        /// <summary>
        /// 复制文件夹及文件
        /// </summary>
        /// <param name="sourceDir">原文件路径</param>
        /// <param name="destinationDir">目标文件路径</param>
        /// <returns></returns>
        public static int CopyDirectory(string sourceDir,
                                        string destinationDir,
                                        bool recursive = true,
                                        bool overwrite = false,
                                        bool deleteTargetFolderBeforeCopy = true,
                                        bool includeSourceDirSelf = false)
        {
            try
            {
                if (string.IsNullOrEmpty(sourceDir))
                {
                    return -1;
                }

                if (includeSourceDirSelf)
                {
                    ///包含源目录自身
                    DirectoryInfo info = new DirectoryInfo(sourceDir);
                    var f = info.Name;
                    destinationDir = Path.GetFullPath(Path.Combine(destinationDir, f));
                }

                if (deleteTargetFolderBeforeCopy && System.IO.Directory.Exists(destinationDir))
                {
                    System.IO.Directory.Delete(destinationDir, true);
                }

                //如果目标路径不存在,则创建目标路径
                if (!System.IO.Directory.Exists(destinationDir))
                {
                    System.IO.Directory.CreateDirectory(destinationDir);
                }

                //得到原文件根目录下的所有文件
                string[] files = System.IO.Directory.GetFiles(sourceDir);
                foreach (string file in files)
                {
                    string name = System.IO.Path.GetFileName(file);
                    string dest = System.IO.Path.Combine(destinationDir, name);
                    System.IO.File.Copy(file, dest, overwrite);//复制文件
                }

                if (recursive)
                {
                    //得到原文件根目录下的所有文件夹
                    string[] folders = System.IO.Directory.GetDirectories(sourceDir);
                    foreach (string folder in folders)
                    {
                        string name = System.IO.Path.GetFileName(folder);
                        string dest = System.IO.Path.Combine(destinationDir, name);
                        var dirName = System.IO.Path.GetDirectoryName(folder);
                        if (name == ".git")
                        {
                            continue;
                        }

                        //构建目标路径,递归复制文件
                        CopyDirectory(folder, dest, recursive, overwrite, deleteTargetFolderBeforeCopy);
                    }
                }

                return 1;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return 0;
            }

        }

    }

    public abstract class CopyInfo
    {
        public abstract List<string> DestinationDirs { get; }

        public void OpenTarget()
        {
            foreach (var target in DestinationDirs)
            {
                var root = Path.Combine(PathUtility.ProjectPath, target);
                var targetFolder = Path.GetFullPath(root);
                Debug.Log($"Open {targetFolder}");
                System.Diagnostics.Process.Start(targetFolder);
            }
        }

        public void DeleteTarget()
        {
            foreach (var target in DestinationDirs)
            {
                var root = Path.Combine(PathUtility.ProjectPath, target);
                var targetFolder = Path.GetFullPath(root);
                Debug.Log($"Delete {targetFolder}");
                if (Directory.Exists(targetFolder))
                {
                    Directory.Delete(targetFolder, true);
                }
            }
        }
    }

    [Serializable]
    public class DirectoryCopyInfo : CopyInfo
    {
        [Path]
        public string Source;

        public bool DeleteTargetFolderBeforeCopy = true;
        public bool IncludeSourceDirSelf = true;

        [Space]
        [Path]
        public List<string> Targets = new();

        public void Copy()
        {
            foreach (string target in DestinationDirs)
            {
                string sourceDir = Source.GetFullPathWithProject();
                string destinationDir = target.GetFullPathWithProject();
                CopyUtility.CopyDirectory(sourceDir, destinationDir, includeSourceDirSelf: IncludeSourceDirSelf);
                Debug.Log($"Copy {sourceDir}  To  {destinationDir}");
            }
        }

        public override List<string> DestinationDirs => Targets;
    }

    [Serializable]
    public class UPMCopyInfo : CopyInfo
    {
        [Space]
        public List<string> packageName = new();

        public bool DeleteTargetFolderBeforeCopy = true;

        [Space]
        [Path]
        public List<string> Targets = new();
        public override List<string> DestinationDirs => Targets;

        public void CopyOne(string packageName, string targetFolder)
        {
            string sourceDir = null;
            string destinationDir = targetFolder.GetFullPathWithProject();

#if UNITY_EDITOR

            var infos = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
            foreach (var info in infos)
            {
                if (packageName.Trim().Equals(info.name, StringComparison.OrdinalIgnoreCase))
                {
                    sourceDir = info.resolvedPath;
                    destinationDir = Path.GetFullPath(Path.Combine(destinationDir, info.name));
                }
            }

#endif

            CopyUtility.CopyDirectory(sourceDir,
                                      destinationDir,
                                      true,
                                      DeleteTargetFolderBeforeCopy);

            Debug.Log($"Copy {sourceDir}  To  {destinationDir}");
        }

        public void Copy()
        {
            foreach (var target in Targets)
            {
                foreach (var item in packageName)
                {
                    CopyOne(item, target);
                }
            }
        }
    }


    public class FastCopy : ScriptableObject
    {
        [FormerlySerializedAs("ops")]
        public List<DirectoryCopyInfo> DirectoryCopy = new List<DirectoryCopyInfo>();
        public List<UPMCopyInfo> UPMCopy = new List<UPMCopyInfo>();

        [ContextMenu("Copy")]
        public void Copy()
        {
            foreach (DirectoryCopyInfo op in DirectoryCopy)
            {
                op.Copy();
            }

            foreach (var item in UPMCopy)
            {
                item.Copy();
            }
        }

        [ContextMenu("OpenTarget")]
        public void OpenTarget()
        {
            foreach (DirectoryCopyInfo op in DirectoryCopy)
            {
                op.OpenTarget();
            }

            foreach (var item in UPMCopy)
            {
                item.OpenTarget();
            }
        }

        //[ContextMenu("DeleteTarget")]
        //public void DeleteTarget()
        //{
        //    foreach (DirectoryCopyInfo op in ops)
        //    {
        //        op.DeleteTarget();
        //    }

        //    foreach (var item in UPMCopy)
        //    {
        //        item.DeleteTarget();
        //    }
        //}
    }
}
