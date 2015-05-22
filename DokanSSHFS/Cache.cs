using System;
using System.Collections.Generic;
using System.IO;
using System.Collections;
using DokanNet;
using System.Security.AccessControl;

namespace DokanSSHFS
{
    class CacheEntry
    {
        public string Name = null;
        public Dictionary<string, CacheEntry> Children = null;

        public DokanError CreateFileRet = DokanError.Undefined;
        public DokanError OpenDirectoryRet = DokanError.Undefined;
        public DokanError GetFileInfoRet = DokanError.Undefined;
        public FileInformation GetFileInfoValue = new FileInformation();
        public DokanError FindFilesRet = DokanError.Undefined;
        public IList<FileInformation> FindFilesValue = null;
        public CacheEntry Parrent = null;

        public CacheEntry(string name)
        {
            Name = name;
            Parrent = this;
        }

        public CacheEntry Lookup(string fullname)
        {
            string[] names = fullname.Split('\\');

            CacheEntry current = this;
            CacheEntry child = null;
            foreach (string entry in names)
            {
                if (current.Children == null)
                    current.Children = new Dictionary<string, CacheEntry>();

                if (current.Children.TryGetValue(entry, out child))
                {
                    current = child;
                }
                else
                {
                    CacheEntry cache = new CacheEntry(entry);
                    current.Children[entry] = cache;
                    cache.Parrent = current;
                    current = cache;
                }
            }

            return current;
        }

        public void RemoveCreateFileCache()
        {
            CreateFileRet = DokanError.Undefined;
        }

        public void RemoveOpenDirectoryCache()
        {
            OpenDirectoryRet = DokanError.Undefined;
        }

        public void RemoveGetFileInfoCache()
        {
            GetFileInfoRet = DokanError.Undefined;
            GetFileInfoValue = new FileInformation();
        }

        public void RemoveFindFilesCache()
        {
            System.Diagnostics.Debug.WriteLine("RemoveFindFilesCache " + Name);
            FindFilesRet = DokanError.Undefined;
            FindFilesValue = null;
            Children = null;
        }

        public void RemoveAllCache()
        {
            RemoveCreateFileCache();
            RemoveFindFilesCache();
            RemoveGetFileInfoCache();
            RemoveOpenDirectoryCache();
            Children = null;
        }
    }



    class CacheOperations : IDokanOperations
    {
        IDokanOperations ope_;
        CacheEntry cache_;

        public CacheOperations(IDokanOperations ope)
        {
            ope_ = ope;
            cache_ = new CacheEntry(null);
        }


        public DokanError CreateFile(string filename, DokanNet.FileAccess access, FileShare share,
            FileMode mode, FileOptions options, FileAttributes attributes, DokanFileInfo info)
        {
            DokanError ret = DokanError.ErrorSuccess;

            if (filename.EndsWith(":SSHFSProperty.Cache"))
            {
                System.Diagnostics.Debug.WriteLine("SSHFS.Cache: " + filename);

                filename = filename.Remove(filename.IndexOf(":SSHFSProperty.Cache"));
                CacheEntry entry = cache_.Lookup(filename);
                entry.RemoveAllCache();
                return DokanError.ErrorSuccess;
            }

            if (mode == FileMode.Open || mode == FileMode.OpenOrCreate)
            {
                CacheEntry entry = cache_.Lookup(filename);

                if (mode == FileMode.OpenOrCreate)
                {
                    if (entry.Parrent != null)
                        entry.Parrent.RemoveFindFilesCache();
                }

                if (entry.CreateFileRet == DokanError.Undefined)
                {
                    ret = ope_.CreateFile(filename, access, share, mode, options, attributes, info);
                    entry.CreateFileRet = ret;
                }
                else
                {
                    ret = entry.CreateFileRet;
                }
            }
            else
            {
                ret = ope_.CreateFile(filename, access, share, mode, options, attributes, info);

                if (mode == FileMode.Create || mode == FileMode.CreateNew)
                {
                    CacheEntry entry = cache_.Lookup(filename);
                    if (entry.Parrent != null)
                        entry.Parrent.RemoveFindFilesCache();
                }
            }
            return ret;
        }


        public DokanError OpenDirectory(string filename, DokanFileInfo info)
        {
            DokanError ret = 0;

            CacheEntry entry = cache_.Lookup(filename);
            if (entry.OpenDirectoryRet == DokanError.Undefined)
            {
                ret = ope_.OpenDirectory(filename, info);
                entry.OpenDirectoryRet = ret;
            }
            else
            {
                ret = entry.OpenDirectoryRet;
            }
            return ret;
        }

        public DokanError CreateDirectory(string filename, DokanFileInfo info)
        {
            CacheEntry entry = cache_.Lookup(filename);

            if (entry.Parrent != null)
            {
                entry.Parrent.RemoveAllCache();
            }
            return ope_.CreateDirectory(filename, info);
        }

        public DokanError Cleanup(string filename, DokanFileInfo info)
        {
            return ope_.Cleanup(filename, info);
        }

        public DokanError CloseFile(string filename, DokanFileInfo info)
        {
            return ope_.CloseFile(filename, info);
        }

        public DokanError ReadFile(string filename, byte[] buffer,
            out int readBytes, long offset, DokanFileInfo info)
        {
            return ope_.ReadFile(filename, buffer, out readBytes, offset, info);
        }

        public DokanError WriteFile(string filename, byte[] buffer,
            out int writtenBytes, long offset, DokanFileInfo info)
        {
            return ope_.WriteFile(filename, buffer, out writtenBytes, offset, info);
        }

        public DokanError FlushFileBuffers(string filename, DokanFileInfo info)
        {
            return ope_.FlushFileBuffers(filename, info);
        }


        public DokanError GetFileInformation(string filename, out FileInformation fileinfo, DokanFileInfo info)
        {
            CacheEntry entry = cache_.Lookup(filename);

            DokanError ret = 0;

            if (entry.GetFileInfoRet == DokanError.Undefined)
            {
                ret = ope_.GetFileInformation(filename, out fileinfo, info);
                entry.GetFileInfoRet = ret;
                entry.GetFileInfoValue = fileinfo;
            }
            else
            {
                FileInformation finfo = entry.GetFileInfoValue;

                fileinfo = new FileInformation();
                fileinfo.Attributes = finfo.Attributes;
                fileinfo.CreationTime = finfo.CreationTime;
                fileinfo.FileName = finfo.FileName;
                fileinfo.LastAccessTime = finfo.LastAccessTime;
                fileinfo.LastWriteTime = finfo.LastWriteTime;
                fileinfo.Length = finfo.Length;

                ret = entry.GetFileInfoRet;
            }

            return ret;
        }


        public DokanError FindFiles(string filename, out IList<FileInformation> files, DokanFileInfo info)
        {
            CacheEntry entry = cache_.Lookup(filename);

            DokanError ret = 0;

            if (entry.FindFilesRet == DokanError.Undefined)
            {
                ret = ope_.FindFiles(filename, out files, info);
                entry.FindFilesRet = ret;
                entry.FindFilesValue = files;
            }
            else
            {
                files = new List<FileInformation>();
                IList<FileInformation> cfiles = entry.FindFilesValue;
                foreach (FileInformation e in cfiles)
                {
                    files.Add(e);
                }

                ret = entry.FindFilesRet;
            }
            return ret;
        }

        public DokanError SetFileAttributes(string filename, FileAttributes attr, DokanFileInfo info)
        {
            CacheEntry entry = cache_.Lookup(filename);
            entry.RemoveGetFileInfoCache();

            return ope_.SetFileAttributes(filename, attr, info);
        }

        public DokanError SetFileTime(string filename, DateTime? ctime, DateTime? atime,
            DateTime? mtime, DokanFileInfo info)
        {
            CacheEntry entry = cache_.Lookup(filename);
            entry.RemoveGetFileInfoCache();

            return ope_.SetFileTime(filename, ctime, atime, mtime, info);
        }

        public DokanError DeleteFile(string filename, DokanFileInfo info)
        {
            CacheEntry entry = cache_.Lookup(filename);

            entry.RemoveAllCache();
            entry.Parrent.RemoveFindFilesCache();

            return ope_.DeleteFile(filename, info);
        }

        public DokanError DeleteDirectory(string filename, DokanFileInfo info)
        {
            CacheEntry entry = cache_.Lookup(filename);

            entry.RemoveAllCache();
            entry.Parrent.RemoveFindFilesCache();

            return ope_.DeleteDirectory(filename, info);
        }

        public DokanError MoveFile(string filename, string newname, bool replace, DokanFileInfo info)
        {
            CacheEntry entry = cache_.Lookup(filename);

            entry.RemoveAllCache();
            entry.Parrent.RemoveFindFilesCache();

            entry = cache_.Lookup(newname);
            entry.RemoveAllCache();
            entry.Parrent.RemoveFindFilesCache();

            return ope_.MoveFile(filename, newname, replace, info);
        }

        public DokanError SetEndOfFile(string filename, long length, DokanFileInfo info)
        {
            CacheEntry entry = cache_.Lookup(filename);
            entry.RemoveGetFileInfoCache();

            return ope_.SetEndOfFile(filename, length, info);
        }

        public DokanError SetAllocationSize(string filename, long length, DokanFileInfo info)
        {
            CacheEntry entry = cache_.Lookup(filename);
            entry.RemoveGetFileInfoCache();

            return ope_.SetAllocationSize(filename, length, info);
        }

        public DokanError LockFile(string filename, long offset, long length, DokanFileInfo info)
        {
            return ope_.LockFile(filename, offset, length, info);
        }

        public DokanError UnlockFile(string filename, long offset, long length, DokanFileInfo info)
        {
            return ope_.UnlockFile(filename, offset, length, info);
        }

        public DokanError GetDiskFreeSpace(
            out long freeBytesAvailable,
            out long totalBytes,
            out long totalFreeBytes,
            DokanFileInfo info)
        {
            return ope_.GetDiskFreeSpace(out freeBytesAvailable, out totalBytes, out totalFreeBytes, info);
        }

        public DokanError Unmount(DokanFileInfo info)
        {
            cache_.RemoveAllCache();

            return ope_.Unmount(info);
        }

        public DokanError GetFileSecurity(string fileName, out FileSystemSecurity security, AccessControlSections sections, DokanFileInfo info)
        {
            return ope_.GetFileSecurity(fileName, out security, sections, info);
        }

        public DokanError SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections, DokanFileInfo info)
        {
            return ope_.SetFileSecurity(fileName, security, sections, info);
        }

        public DokanError GetVolumeInformation(out string volumeLabel, out FileSystemFeatures features, out string fileSystemName, DokanFileInfo info)
        {
            return ope_.GetVolumeInformation(out volumeLabel, out features, out fileSystemName, info);
        }
    }
}
