using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// ReSharper disable once CheckNamespace
namespace BuildMapBasicProject
{
	class Program
	{
		public static bool IsCmdLineArg(string arg, params string[] options)
		{
			return options.Aggregate(false, (current, option) => current || MatchesAny(arg, "-" + option, "/" + option));
		}

		public static bool MatchesAny(string arg, params string[] options)
		{
			return arg != null && options != null && options.Any(p => string.Compare(p, arg, StringComparison.OrdinalIgnoreCase) == 0);
		}

		// If there is a project file compile mb files from module list into mbo then link project
		// If no project file then we just compile the mb files in the passed in folder
		static int Main(string[] args)
		{
            // Validate arguments and show usage message
            if (args.Length == 0 || args.Length > 2)
            {                
                Console.WriteLine($"Usage: MapBasicHelper.exe [PathToMapBasic.exe] FolderToBuild");
                return 1;
            }
			var compiler = new CompileMb();
			compiler.FindMapBasic();

			foreach (var arg in args)
			{
				if (arg.EndsWith("MapBasic.exe", StringComparison.OrdinalIgnoreCase))
				{
					compiler.MapBasicExe = arg;
				}
				else
				{
					compiler.OutputFolder = arg;
				}
			}
			foreach (var file in Directory.EnumerateFiles(compiler.OutputFolder))
			{
				if (file.EndsWith(".mb", StringComparison.OrdinalIgnoreCase))
				{
					compiler.SourceFiles.Add(file);
				}
				if (file.EndsWith(".mbp", StringComparison.OrdinalIgnoreCase))
				{
					compiler.ProjectFile = file;
				}
			}
			if (compiler.Execute())
			{
				Console.WriteLine("No errors found");
				return 0;
			}
			Console.WriteLine("Errors found");
			return 1;
		}
	}
	public class CompileMb
	{
		public string MapBasicExe { get; set; }
		public string MapBasicArguments { get; set; }
		public string OutputFolder { get; set; }
		public string IntermediateFolder { get; set; }

		public List<string> SourceFiles { get; } = new List<string>();

		public string ProjectFile { get; set; }

		public CompileMb()
		{
			MapBasicArguments = "-NOSPLASH -server -nodde ";
		}

		public void FindMapBasic()
		{
			if (string.IsNullOrWhiteSpace(MapBasicExe) || !File.Exists(MapBasicExe))
			{
				MapBasicExe = Environment.GetEnvironmentVariable("MAPBASICEXE"); // allow for local env to override registry
				if (File.Exists(MapBasicExe))
				{
					return;
				}

				var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\mapbasic.exe")?.GetValue(null);
				if (key != null)
				{
					MapBasicExe = key.ToString();
				}
				if (!File.Exists(MapBasicExe))
				{
					MapBasicExe = @"C:\Program Files\MapInfo\MapBasic\mapbasic.exe";
				}
			}
		}

		public static bool VerifyMbp(IniFile project)
		{
			if (project == null)
				throw new ArgumentNullException();

			if (project["Link"] == null)
			{
				// write to stderr - with line #
			}
			//	throw new NoLinkSectionException(string.Format(Resources.PreCompiler_Invalid_Project_File, Resources.PreCompiler_No_Link_Section));

			//if (project["Link"]["Application"] == null)
			//	throw new NoApplicationKeyException(string.Format(Resources.PreCompiler_Invalid_Project_File, Resources.PreCompiler_No_Module));

			//if (project["Link"]["Module"] == null)
			//	throw new NoModuleFoundException(string.Format(Resources.PreCompiler_Invalid_Project_File, Resources.PreCompiler_No_Application_Name));
			return true;
		}

		public bool Execute()
		{
			bool errors = false;

			if (string.IsNullOrWhiteSpace(MapBasicExe) || !File.Exists(MapBasicExe))
			{
				Console.Error.WriteLine("MapBasicExe not specified or not found. Path='{0}'", MapBasicExe);
				return false;
			}

			var startInfo = new System.Diagnostics.ProcessStartInfo
			{
				CreateNoWindow = true,
				UseShellExecute = false,
				FileName = MapBasicExe,
				//WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
				Arguments = MapBasicArguments,
				WorkingDirectory = Path.GetDirectoryName(OutputFolder) ?? throw new InvalidOperationException()
			};

			if (!string.IsNullOrEmpty(ProjectFile))
			{
				// we have a project file, then only compile mbs from it, not from folder
				SourceFiles.Clear();
				// load project file and add modules to list of files to be built
				// check for existence of .mb file
				var projectIni = new IniFile(ProjectFile);
				if (!VerifyMbp(projectIni))
				{
					return false;
				}
				// get application=mbxname
				foreach (var val in projectIni["Link"]["Module"].Value)
				{
					var mb = Path.Combine(OutputFolder, val);
					mb = Path.ChangeExtension(mb, ".mb");
					if (File.Exists(mb))
					{
						SourceFiles.Add(mb);
					}
				}

			}
			foreach (var item in SourceFiles)
			{
				if (item == null) continue;
				startInfo.Arguments += " -d " + item;
			}

			if (!string.IsNullOrWhiteSpace(ProjectFile))
			{
				startInfo.Arguments += " -l " + ProjectFile;
			}
			try
			{
				// Start the process with the info we specified.
				// Call WaitForExit and then the using statement will close.
				using (System.Diagnostics.Process exeProcess = System.Diagnostics.Process.Start(startInfo))
				{
					exeProcess?.WaitForExit();
				}


				errors = ProcessErrors(OutputFolder);

			}
			catch (Exception)
			{
				//TODO: write an error to stderr
				errors = true;
			}
			return !errors;
		}

		// returns true if errors
		bool ProcessErrors(string folder)
		{
			// Put all err file names in current directory.
			string[] errFiles = Directory.GetFiles(folder, @"*.err");
			bool errors = false;
			foreach (var errFile in errFiles)
			{
				using (StreamReader r = new StreamReader(errFile))
				{
					string errLine;
					while ((errLine = r.ReadLine()) != null)
					{
						// sample error:  (prospy.mb:72) Found: [End ] while searching for [End Program], [End MapInfo], or [End function]. 
						if (!errLine.StartsWith("("))
						{
							errLine = $"({Path.GetFileNameWithoutExtension(errFile)}.mbp:1)" + errLine;
						}
						Console.Error.WriteLine(errLine);
						errors = true;
					}
				}
			}
			return errors;
		}

	}

}
