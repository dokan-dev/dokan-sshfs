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

        public NtStatus CreateFileRet = NtStatus.MaximumNtStatus;
        public NtStatus GetFileInfoRet = NtStatus.MaximumNtStatus;
        public FileInformation GetFileInfoValue = new FileInformation();
        public NtStatus FindFilesRet = NtStatus.MaximumNtStatus;
        public NtStatus FindStreamsRet = NtStatus.MaximumNtStatus;
        public IList<FileInformation> FindFilesValue = null;
        public IList<FileInformation> FindStreamsValue = null;
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
            CreateFileRet = NtStatus.MaximumNtStatus;
        }

        public void RemoveGetFileInfoCache()
        {
            GetFileInfoRet = NtStatus.MaximumNtStatus;
            GetFileInfoValue = new FileInformation();
        }

        public void RemoveFindStreamsCache()
        {
            FindStreamsRet = NtStatus.MaximumNtStatus;
            FindStreamsValue = null;
        }

        public void RemoveFindFilesCache()
        {
            System.Diagnostics.Debug.WriteLine("RemoveFindFilesCache " + Name);
            FindFilesRet = NtStatus.MaximumNtStatus;
            FindFilesValue = null;
            Children = null;
        }

        public void RemoveAllCache()
        {
            RemoveCreateFileCache();
            RemoveFindFilesCache();
            RemoveFindStreamsCache();
            RemoveGetFileInfoCache();
            
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


        public NtStatus CreateFile(string filename, DokanNet.FileAccess access, FileShare share,
            FileMode mode, FileOptions options, FileAttributes attributes, DokanFileInfo info)
        {
            NtStatus ret = NtStatus.Success;

            if (filename.EndsWith(":SSHFSProperty.Cache"))
            {
                System.Diagnostics.Debug.WriteLine("SSHFS.Cache: " + filename);

                filename = filename.Remove(filename.IndexOf(":SSHFSProperty.Cache"));
                CacheEntry entry = cache_.Lookup(filename);
                entry.RemoveAllCache();
                return NtStatus.Success;
            }



            if (mode == FileMode.Open || mode == FileMode.OpenOrCreate)
            {
                CacheEntry entry = cache_.Lookup(filename);

                if (mode == FileMode.OpenOrCreate)
                {
                    if (entry.Parrent != null)
                        entry.Parrent.RemoveFindFilesCache();
                }

                if (entry.CreateFileRet == NtStatus.MaximumNtStatus)
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

        public void Cleanup(string filename, DokanFileInfo info)
        {
            ope_.Cleanup(filename, info);
        }

        public void CloseFile(string filename, DokanFileInfo info)
        {
            ope_.CloseFile(filename, info);
        }

        public NtStatus ReadFile(string filename, byte[] buffer,
            out int readBytes, long offset, DokanFileInfo info)
        {
            return ope_.ReadFile(filename, buffer, out readBytes, offset, info);
        }

        public NtStatus WriteFile(string filename, byte[] buffer,
            out int writtenBytes, long offset, DokanFileInfo info)
        {
            return ope_.WriteFile(filename, buffer, out writtenBytes, offset, info);
        }

        public NtStatus FlushFileBuffers(string filename, DokanFileInfo info)
        {
            return ope_.FlushFileBuffers(filename, info);
        }


        public NtStatus GetFileInformation(string filename, out FileInformation fileinfo, DokanFileInfo info)
        {
            CacheEntry entry = cache_.Lookup(filename);

            NtStatus ret = 0;

            if (entry.GetFileInfoRet == NtStatus.MaximumNtStatus)
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


        public NtStatus FindFilesWithPattern(string filename, string searchPattern, out IList<FileInformation> files, DokanFileInfo info)
        {
            // TODO : not cached, since searchPattern may change
            // alternatively, call FileFiles and search using searchPattern over cached result.
            // however, first we need to expose the DokanIsNameInExpression API function
            // or implement it ourselves in DokanNet
            return ope_.FindFilesWithPattern(filename, searchPattern, out files, info);
        }


        public NtStatus FindFiles(string filename, out IList<FileInformation> files, DokanFileInfo info)
        {
            CacheEntry entry = cache_.Lookup(filename);

            NtStatus ret = 0;

            if (entry.FindFilesRet == NtStatus.MaximumNtStatus)
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

        public NtStatus FindStreams(string filename, out IList<FileInformation> streams, DokanFileInfo info)
        {
            CacheEntry entry = cache_.Lookup(filename);

            NtStatus ret = 0;

            if (entry.FindStreamsRet == NtStatus.MaximumNtStatus)
            {
                ret = ope_.FindStreams(filename, out streams, info);
                entry.FindStreamsRet = ret;
                entry.FindStreamsValue = streams;
            }
            else
            {
                streams = new List<FileInformation>();
                IList<FileInformation> cfiles = entry.FindStreamsValue;
                foreach (FileInformation e in cfiles)
                {
                    streams.Add(e);
                }

                ret = entry.FindStreamsRet;
            }
            return ret;
        }



        public NtStatus SetFileAttributes(string filename, FileAttributes attr, DokanFileInfo info)
        {
            CacheEntry entry = cache_.Lookup(filename);
            entry.RemoveGetFileInfoCache();

            return ope_.SetFileAttributes(filename, attr, info);
        }

        public NtStatus SetFileTime(string filename, DateTime? ctime, DateTime? atime,
            DateTime? mtime, DokanFileInfo info)
        {
            CacheEntry entry = cache_.Lookup(filename);
            entry.RemoveGetFileInfoCache();

            return ope_.SetFileTime(filename, ctime, atime, mtime, info);
        }

        public NtStatus DeleteFile(string filename, DokanFileInfo info)
        {
            CacheEntry entry = cache_.Lookup(filename);

            entry.RemoveAllCache();
            entry.Parrent.RemoveFindFilesCache();

            return ope_.DeleteFile(filename, info);
        }

        public NtStatus DeleteDirectory(string filename, DokanFileInfo info)
        {
            CacheEntry entry = cache_.Lookup(filename);

            entry.RemoveAllCache();
            entry.Parrent.RemoveFindFilesCache();

            return ope_.DeleteDirectory(filename, info);
        }

        public NtStatus MoveFile(string filename, string newname, bool replace, DokanFileInfo info)
        {
            CacheEntry entry = cache_.Lookup(filename);

            entry.RemoveAllCache();
            entry.Parrent.RemoveFindFilesCache();

            entry = cache_.Lookup(newname);
            entry.RemoveAllCache();
            entry.Parrent.RemoveFindFilesCache();

            return ope_.MoveFile(filename, newname, replace, info);
        }

        public NtStatus SetEndOfFile(string filename, long length, DokanFileInfo info)
        {
            CacheEntry entry = cache_.Lookup(filename);
            entry.RemoveGetFileInfoCache();

            return ope_.SetEndOfFile(filename, length, info);
        }

        public NtStatus SetAllocationSize(string filename, long length, DokanFileInfo info)
        {
            CacheEntry entry = cache_.Lookup(filename);
            entry.RemoveGetFileInfoCache();

            return ope_.SetAllocationSize(filename, length, info);
        }

        public NtStatus LockFile(string filename, long offset, long length, DokanFileInfo info)
        {
            return ope_.LockFile(filename, offset, length, info);
        }

        public NtStatus UnlockFile(string filename, long offset, long length, DokanFileInfo info)
        {
            return ope_.UnlockFile(filename, offset, length, info);
        }

        public NtStatus GetDiskFreeSpace(
            out long freeBytesAvailable,
            out long totalBytes,
            out long totalFreeBytes,
            DokanFileInfo info)
        {
            return ope_.GetDiskFreeSpace(out freeBytesAvailable, out totalBytes, out totalFreeBytes, info);
        }

        public NtStatus Mounted(DokanFileInfo info)
        {
            cache_.RemoveAllCache();

            return ope_.Mounted(info);
        }

        public NtStatus Unmounted(DokanFileInfo info)
        {
            cache_.RemoveAllCache();

            return ope_.Unmounted(info);
        }

        public NtStatus GetFileSecurity(string fileName, out FileSystemSecurity security, AccessControlSections sections, DokanFileInfo info)
        {
            return ope_.GetFileSecurity(fileName, out security, sections, info);
        }

        public NtStatus SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections, DokanFileInfo info)
        {
            return ope_.SetFileSecurity(fileName, security, sections, info);
        }

        public NtStatus GetVolumeInformation(out string volumeLabel, out FileSystemFeatures features, out string fileSystemName, out uint maximumComponentLength, DokanFileInfo info)
        {
            return ope_.GetVolumeInformation(out volumeLabel, out features, out fileSystemName, out maximumComponentLength, info);
        }
    }
}
