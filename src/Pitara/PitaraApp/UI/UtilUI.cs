using System;
using System.Windows.Forms;
namespace Pitara
{
    internal static class UtilUI
    {
        public static string LetUserPickAFolder(string selectFolder, string title)
        {
            using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
            {
                fbd.SelectedPath = selectFolder;
                fbd.ShowNewFolderButton = true;
                fbd.Description = title;
                fbd.SelectedPath = selectFolder;
                // fbd.RootFolder = Environment.SpecialFolder.MyComputer;
                System.Windows.Forms.DialogResult result = fbd.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    return fbd.SelectedPath.TrimEnd('\\') + @"\"; ;
                }
                else
                {
                    return null;
                }
            }
        }

    }
}
