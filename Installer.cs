using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

public class NfoSetup : Form {
    readonly string target=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"NFO Maker");
    readonly Button button=new Button();
    readonly Label status=new Label();
    bool installed;
    [DllImport("shell32.dll")] static extern void SHChangeNotify(uint e,uint f,IntPtr a,IntPtr b);
    static readonly string[] Extensions={".mkv",".mp4",".avi",".m4v",".mov",".wmv",".mpg",".mpeg",".ts",".m2ts",".iso",".strm"};
    public NfoSetup(){
        AppBranding.SetIcon(this);
        Text="Install NFO Maker";Font=new Font("Segoe UI",10);Size=new Size(610,350);MinimumSize=Size;MaximumSize=Size;AutoScaleMode=AutoScaleMode.Dpi;StartPosition=FormStartPosition.CenterScreen;BackColor=Color.White;
        var panel=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(22),ColumnCount=1,RowCount=5};Controls.Add(panel);
        panel.Controls.Add(new Label{Text="NFO Maker",Font=new Font("Segoe UI",20,FontStyle.Bold),AutoSize=true});
        panel.Controls.Add(new Label{Text="Free movie and TV metadata editor. No accounts or subscriptions.",AutoSize=true});
        panel.Controls.Add(new Label{Text="Installs to:\n"+target+"\n\nCreates Start and desktop shortcuts, and adds NFO actions to video and folder right-click menus. Your default video player stays unchanged.",AutoSize=true,MaximumSize=new Size(550,0),Padding=new Padding(0,12,0,10)});
        status.AutoSize=true;status.MaximumSize=new Size(550,0);panel.Controls.Add(status);
        button.Text="Install";button.Size=new Size(130,36);button.Anchor=AnchorStyles.Right;button.Click+=delegate{Install();};panel.Controls.Add(button);
    }
    static void Shortcut(string file,string exe){
        Type shellType=Type.GetTypeFromProgID("WScript.Shell");object shell=Activator.CreateInstance(shellType);
        object link=shellType.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{file});Type t=link.GetType();
        t.InvokeMember("TargetPath",BindingFlags.SetProperty,null,link,new object[]{exe});t.InvokeMember("WorkingDirectory",BindingFlags.SetProperty,null,link,new object[]{Path.GetDirectoryName(exe)});t.InvokeMember("Save",BindingFlags.InvokeMethod,null,link,null);
        Marshal.FinalReleaseComObject(link);Marshal.FinalReleaseComObject(shell);
    }
    void Install(){
        if(installed){Close();return;}button.Enabled=false;
        try{
            Directory.CreateDirectory(target);
            using(var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
            using(var zip=new ZipArchive(resource,ZipArchiveMode.Read))foreach(var entry in zip.Entries){
                if(entry.FullName!="NfoMaker.exe"&&entry.FullName!="README.md"&&entry.FullName!="LICENSE")throw new InvalidDataException("Unexpected installer file");
                using(var input=entry.Open())using(var output=File.Create(Path.Combine(target,entry.FullName)))input.CopyTo(output);
            }
            string exe=Path.Combine(target,"NfoMaker.exe"), command="\""+exe+"\" \"%1\"";
            Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),"NFO Maker.lnk"),exe);
            Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),"NFO Maker.lnk"),exe);
            foreach(string ext in Extensions){
                string key=@"Software\Classes\SystemFileAssociations\"+ext+@"\shell\NfoMaker.Create";
                using(var k=Registry.LocalMachine.CreateSubKey(key)){k.SetValue("","Create NFO file");k.SetValue("Icon","\""+exe+"\",0");k.SetValue("MultiSelectModel","Single");k.SetValue("Position","Top");}
                using(var k=Registry.LocalMachine.CreateSubKey(key+@"\command"))k.SetValue("",command);
            }
            foreach(string location in new [] { @"Directory\shell\NfoMaker.Batch", @"Directory\Background\shell\NfoMaker.Batch" }) {
                string key=@"Software\Classes\"+location;
                using(var k=Registry.LocalMachine.CreateSubKey(key)){k.SetValue("","NFO Maker");k.SetValue("Icon","\""+exe+"\",0");k.SetValue("MultiSelectModel","Single");k.SetValue("Position","Top");}
                string argument=location.Contains("Background") ? "%V" : "%1";
                using(var k=Registry.LocalMachine.CreateSubKey(key+@"\command"))k.SetValue("","\""+exe+"\" \""+argument+"\"");
            }
            using(var k=Registry.LocalMachine.CreateSubKey(@"Software\Classes\NfoMaker.Metadata"))k.SetValue("","NFO Maker metadata");
            using(var k=Registry.LocalMachine.CreateSubKey(@"Software\Classes\NfoMaker.Metadata\shell\open\command"))k.SetValue("",command);
            using(var k=Registry.LocalMachine.CreateSubKey(@"Software\Classes\.nfo\OpenWithProgids"))k.SetValue("NfoMaker.Metadata",new byte[0],RegistryValueKind.None);
            using(var k=Registry.LocalMachine.CreateSubKey(@"Software\Classes\Applications\NfoMaker.exe\shell\open\command"))k.SetValue("",command);
            using(var k=Registry.LocalMachine.CreateSubKey(@"Software\Classes\Applications\NfoMaker.exe\SupportedTypes"))k.SetValue(".nfo","");
            string uninstall=@"$ErrorActionPreference = 'Stop'
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Start-Process -FilePath (Join-Path $PSHOME 'powershell.exe') -Verb RunAs -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-File',('""' + $PSCommandPath + '""'))
    exit
}
$root = $PSScriptRoot
$app = Join-Path $root 'NfoMaker.exe'
if (Get-Process NfoMaker -ErrorAction SilentlyContinue) { throw 'Close NFO Maker before uninstalling.' }
foreach ($ext in @('.mkv','.mp4','.avi','.m4v','.mov','.wmv','.mpg','.mpeg','.ts','.m2ts','.iso','.strm')) {
    $key = 'HKLM:\Software\Classes\SystemFileAssociations\'+$ext+'\shell\NfoMaker.Create'
    if (Test-Path -LiteralPath ($key+'\command')) {
        $cmd = (Get-Item -LiteralPath ($key+'\command')).GetValue('')
        if ($cmd -eq ('""'+$app+'"" ""%1""')) { Remove-Item -LiteralPath $key -Recurse }
    }
}
foreach ($location in @('Directory\shell\NfoMaker.Batch','Directory\Background\shell\NfoMaker.Batch')) {
    $key = 'HKLM:\Software\Classes\' + $location
    $argument = if ($location.Contains('Background')) { '%V' } else { '%1' }
    if (Test-Path -LiteralPath ($key+'\command')) {
        if ((Get-Item -LiteralPath ($key+'\command')).GetValue('') -eq ('""'+$app+'"" ""'+$argument+'""')) { Remove-Item -LiteralPath $key -Recurse }
    }
}
$metadataKey = 'HKLM:\Software\Classes\NfoMaker.Metadata'
if (Test-Path -LiteralPath ($metadataKey+'\shell\open\command')) {
    if ((Get-Item -LiteralPath ($metadataKey+'\shell\open\command')).GetValue('') -eq ('""'+$app+'"" ""%1""')) {
        Remove-Item -LiteralPath $metadataKey -Recurse
        Remove-ItemProperty -LiteralPath 'HKLM:\Software\Classes\.nfo\OpenWithProgids' -Name 'NfoMaker.Metadata' -ErrorAction SilentlyContinue
    }
}
$applicationKey = 'HKLM:\Software\Classes\Applications\NfoMaker.exe'
if (Test-Path -LiteralPath ($applicationKey+'\shell\open\command')) {
    if ((Get-Item -LiteralPath ($applicationKey+'\shell\open\command')).GetValue('') -eq ('""'+$app+'"" ""%1""')) { Remove-Item -LiteralPath $applicationKey -Recurse }
}
foreach ($folder in @([Environment]::GetFolderPath('CommonPrograms'),[Environment]::GetFolderPath('CommonDesktopDirectory'))) { $link=Join-Path $folder 'NFO Maker.lnk'; if (Test-Path -LiteralPath $link) { Remove-Item -LiteralPath $link } }
foreach ($name in @('NfoMaker.exe','README.md','LICENSE')) { $file=Join-Path $root $name; if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file } }
Remove-Item -LiteralPath 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\NfoMaker' -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $PSCommandPath
";
            string script=Path.Combine(target,"Uninstall.ps1");File.WriteAllText(script,uninstall,new UTF8Encoding(true));
            using(var k=Registry.LocalMachine.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\NfoMaker")){
                k.SetValue("DisplayName","NFO Maker");k.SetValue("DisplayVersion","1.0.3");k.SetValue("Publisher","Brett Martin Moore");k.SetValue("InstallLocation",target);k.SetValue("DisplayIcon",exe);k.SetValue("NoModify",1,RegistryValueKind.DWord);k.SetValue("NoRepair",1,RegistryValueKind.DWord);
                string ps=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe");k.SetValue("UninstallString","\""+ps+"\" -NoProfile -ExecutionPolicy Bypass -File \""+script+"\"");
            }
            SHChangeNotify(0x08000000,0,IntPtr.Zero,IntPtr.Zero);
            status.Text="Installed. Open NFO Maker from Start or your desktop.\nOn Windows 11, the NFO actions may appear under Show more options.";installed=true;button.Text="Close";button.Enabled=true;
        }catch(Exception e){status.Text="Installation did not finish: "+e.Message;button.Enabled=true;}
    }
    [STAThread] public static void Main(){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new NfoSetup());}
}
