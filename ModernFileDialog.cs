using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace CheryTools
{
    /// <summary>
    /// 现代化的 Windows 文件对话框封装（基于 IFileDialog COM 接口）
    /// </summary>
    public static class ModernFileDialog
    {
        public static string ShowOpenFileDialog(string title, string filter, string initialDirectory)
        {
            IFileOpenDialog dialog = null;
            try
            {
                dialog = (IFileOpenDialog)new FileOpenDialog();
                
                if (!string.IsNullOrEmpty(title))
                    dialog.SetTitle(title);
                
                if (!string.IsNullOrEmpty(initialDirectory))
                {
                    IShellItem folderItem;
                    Guid iid = new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe");
                    if (SHCreateItemFromParsingName(initialDirectory, IntPtr.Zero, ref iid, out folderItem) == 0)
                    {
                        dialog.SetFolder(folderItem);
                        Marshal.ReleaseComObject(folderItem);
                    }
                }
                
                if (!string.IsNullOrEmpty(filter))
                {
                    var filterSpecs = ParseFilter(filter);
                    if (filterSpecs.Length > 0)
                        dialog.SetFileTypes((uint)filterSpecs.Length, filterSpecs);
                }
                
                uint hr = dialog.Show(IntPtr.Zero);
                if (hr == 0)
                {
                    IShellItem item;
                    dialog.GetResult(out item);
                    IntPtr pszFilePath;
                    item.GetDisplayName(SIGDN.FILESYSPATH, out pszFilePath);
                    string filePath = Marshal.PtrToStringUni(pszFilePath);
                    Marshal.FreeCoTaskMem(pszFilePath);
                    Marshal.ReleaseComObject(item);
                    return filePath;
                }
                
                return null;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (dialog != null)
                    Marshal.ReleaseComObject(dialog);
            }
        }
        
        public static string ShowSaveFileDialog(string title, string filter, string initialDirectory, string defaultFileName)
        {
            IFileSaveDialog dialog = null;
            try
            {
                dialog = (IFileSaveDialog)new FileSaveDialog();
                
                if (!string.IsNullOrEmpty(title))
                    dialog.SetTitle(title);
                
                if (!string.IsNullOrEmpty(defaultFileName))
                    dialog.SetFileName(defaultFileName);
                
                if (!string.IsNullOrEmpty(initialDirectory))
                {
                    IShellItem folderItem;
                    Guid iid = new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe");
                    if (SHCreateItemFromParsingName(initialDirectory, IntPtr.Zero, ref iid, out folderItem) == 0)
                    {
                        dialog.SetFolder(folderItem);
                        Marshal.ReleaseComObject(folderItem);
                    }
                }
                
                if (!string.IsNullOrEmpty(filter))
                {
                    var filterSpecs = ParseFilter(filter);
                    if (filterSpecs.Length > 0)
                        dialog.SetFileTypes((uint)filterSpecs.Length, filterSpecs);
                }
                
                uint hr = dialog.Show(IntPtr.Zero);
                if (hr == 0)
                {
                    IShellItem item;
                    dialog.GetResult(out item);
                    IntPtr pszFilePath;
                    item.GetDisplayName(SIGDN.FILESYSPATH, out pszFilePath);
                    string filePath = Marshal.PtrToStringUni(pszFilePath);
                    Marshal.FreeCoTaskMem(pszFilePath);
                    Marshal.ReleaseComObject(item);
                    return filePath;
                }
                
                return null;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (dialog != null)
                    Marshal.ReleaseComObject(dialog);
            }
        }
        
        private static COMDLG_FILTERSPEC[] ParseFilter(string filter)
        {
            // 解析 "描述 (*.ext)|*.ext" 格式
            if (string.IsNullOrEmpty(filter))
                return new COMDLG_FILTERSPEC[0];
            
            string[] parts = filter.Split('|');
            if (parts.Length % 2 != 0)
                return new COMDLG_FILTERSPEC[0];
            
            int count = parts.Length / 2;
            COMDLG_FILTERSPEC[] specs = new COMDLG_FILTERSPEC[count];
            
            for (int i = 0; i < count; i++)
            {
                specs[i].pszName = parts[i * 2];
                specs[i].pszSpec = parts[i * 2 + 1];
            }
            
            return specs;
        }
        
        #region COM Interfaces
        
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct COMDLG_FILTERSPEC
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pszName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pszSpec;
        }
        
        private enum SIGDN : uint
        {
            FILESYSPATH = 0x80058000
        }
        
        [ComImport]
        [Guid("42f85136-db7e-439c-85f1-e4075d135fc8")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileOpenDialog
        {
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            [PreserveSig]
            uint Show([In] IntPtr hwndOwner);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void SetFileTypes([In] uint cFileTypes, [In, MarshalAs(UnmanagedType.LPArray)] COMDLG_FILTERSPEC[] rgFilterSpec);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void SetFileTypeIndex([In] uint iFileType);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void GetFileTypeIndex(out uint piFileType);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void Advise([In, MarshalAs(UnmanagedType.Interface)] IntPtr pfde, out uint pdwCookie);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void Unadvise([In] uint dwCookie);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void SetOptions([In] uint fos);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void GetOptions(out uint pfos);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void SetDefaultFolder([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void SetFolder([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void GetFolder([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void GetCurrentSelection([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void SetFileName([In, MarshalAs(UnmanagedType.LPWStr)] string pszName);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void SetTitle([In, MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void SetOkButtonLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszText);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void SetFileNameLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void GetResult([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void AddPlace([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, int fdap);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void SetDefaultExtension([In, MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void Close([MarshalAs(UnmanagedType.Error)] int hr);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void SetClientGuid([In] ref Guid guid);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void ClearClientData();
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void SetFilter([MarshalAs(UnmanagedType.Interface)] IntPtr pFilter);
        }
        
        [ComImport]
        [Guid("84bccd23-5fde-4cdb-aea4-af64b83d78ab")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileSaveDialog
        {
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            [PreserveSig]
            uint Show([In] IntPtr hwndOwner);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void SetFileTypes([In] uint cFileTypes, [In, MarshalAs(UnmanagedType.LPArray)] COMDLG_FILTERSPEC[] rgFilterSpec);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void SetFileTypeIndex([In] uint iFileType);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void GetFileTypeIndex(out uint piFileType);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void Advise([In, MarshalAs(UnmanagedType.Interface)] IntPtr pfde, out uint pdwCookie);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void Unadvise([In] uint dwCookie);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void SetOptions([In] uint fos);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void GetOptions(out uint pfos);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void SetDefaultFolder([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void SetFolder([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void GetFolder([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void GetCurrentSelection([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void SetFileName([In, MarshalAs(UnmanagedType.LPWStr)] string pszName);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void SetTitle([In, MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void SetOkButtonLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszText);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void SetFileNameLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void GetResult([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void AddPlace([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, int fdap);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void SetDefaultExtension([In, MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void Close([MarshalAs(UnmanagedType.Error)] int hr);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void SetClientGuid([In] ref Guid guid);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void ClearClientData();
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void SetFilter([MarshalAs(UnmanagedType.Interface)] IntPtr pFilter);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void SetSaveAsItem([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void SetProperties([In, MarshalAs(UnmanagedType.Interface)] IntPtr pStore);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void SetCollectedProperties([In, MarshalAs(UnmanagedType.Interface)] IntPtr pList, [In] int fAppendDefault);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void GetProperties([MarshalAs(UnmanagedType.Interface)] out IntPtr ppStore);
            
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void ApplyProperties([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, [In, MarshalAs(UnmanagedType.Interface)] IntPtr pStore, [In] IntPtr hwnd, [In, MarshalAs(UnmanagedType.Interface)] IntPtr pSink);
        }
        
        [ComImport]
        [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler([In] IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, out IntPtr ppv);
            void GetParent([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);
            void GetDisplayName([In] SIGDN sigdnName, out IntPtr ppszName);
            void GetAttributes([In] uint sfgaoMask, out uint psfgaoAttribs);
            void Compare([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, [In] uint hint, out int piOrder);
        }
        
        [ComImport]
        [Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
        private class FileOpenDialog
        {
        }
        
        [ComImport]
        [Guid("C0B4E2F3-BA21-4773-8DBA-335EC946EB8B")]
        private class FileSaveDialog
        {
        }
        
        private static readonly Guid IID_IShellItem = new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe");
        
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern int SHCreateItemFromParsingName(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            [In] IntPtr pbc,
            [In] ref Guid riid,
            [Out][MarshalAs(UnmanagedType.Interface, IidParameterIndex = 2)] out IShellItem ppv);
        
        #endregion
    }
}
