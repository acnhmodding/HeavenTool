using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace HeavenTool.Utility
{
    public class ProgramAssociation
    {
        private const int ASSOCF_NONE = 0;
        private const int ASSOCSTR_EXECUTABLE = 2;

        [DllImport("Shlwapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern uint AssocQueryString(int flags, int str, string pszAssoc, string pszExtra, [Out] StringBuilder pszOut, ref uint pcchOut);

        public static string GetAssociatedProgram(string extension)
        {
            uint length = 0;
            // Primeira chamada para obter o tamanho necessário
            AssocQueryString(ASSOCF_NONE, ASSOCSTR_EXECUTABLE, extension, null, null, ref length);

            StringBuilder sb = new StringBuilder((int)length);
            // Segunda chamada para obter o programa associado
            AssocQueryString(ASSOCF_NONE, ASSOCSTR_EXECUTABLE, extension, null, sb, ref length);

            return sb.ToString();
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern void SHChangeNotify(uint wEventId, uint uFlags, nint dwItem1, nint dwItem2);

        private const int SHCNE_ASSOCCHANGED = 0x8000000;
        private const int SHCNF_FLUSH = 0x1000;
        public static void AssociateProgram(string extension, string key_name, string description)
        {
            try
            {
                var openWith = Application.ExecutablePath;

                using (var BaseKey = Registry.ClassesRoot.CreateSubKey(extension))
                {
                    BaseKey.SetValue("", key_name);

                    using (var OpenMethod = Registry.ClassesRoot.CreateSubKey(key_name))
                    {
                        OpenMethod.SetValue("", description);
                        OpenMethod.CreateSubKey("DefaultIcon").SetValue("", $"\"{openWith}\",0");

                        using (var Shell = OpenMethod.CreateSubKey("Shell"))
                        {
                            Shell.CreateSubKey("edit").CreateSubKey("command").SetValue("", $"\"{openWith}\" \"%1\"");
                            Shell.CreateSubKey("open").CreateSubKey("command").SetValue("", $"\"{openWith}\" \"%1\"");
                        }
                    }
                }

                using (var CurrentUser = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\FileExts\\" + extension, true))
                    CurrentUser.DeleteSubKey("UserChoice", false);


                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_FLUSH, nint.Zero, nint.Zero);
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("You need to open the program as administrator.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            catch (Exception error)
            {
                MessageBox.Show(error.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static void DisassociateProgram(string extension, string key_name)
        {
            try
            {
                using (var BaseKey = Registry.ClassesRoot.OpenSubKey(extension, true))
                {
                    if (BaseKey != null)
                        Registry.ClassesRoot.DeleteSubKeyTree(extension, false);
                }

                using (var OpenMethodKey = Registry.ClassesRoot.OpenSubKey(key_name, true))
                {
                    if (OpenMethodKey != null)
                        Registry.ClassesRoot.DeleteSubKeyTree(key_name, false);

                }

                using (var CurrentUser = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\FileExts\\" + extension, true))
                    CurrentUser?.DeleteSubKey("UserChoice", false);


                SHChangeNotify(0x08000000, 0x0000, nint.Zero, nint.Zero);
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("Você precisa abrir o programa como administrador.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString(), "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
