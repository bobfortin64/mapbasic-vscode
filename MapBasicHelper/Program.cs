using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.VisualBasic;

// ReSharper disable once CheckNamespace
namespace BuildMapBasicProject
{
	class Program
	{
		#region COM Stuff

		[System.Runtime.InteropServices.DllImport("ole32.dll")]
		private static extern int CreateBindCtx(int reserved, out IBindCtx ppbc);

		[System.Runtime.InteropServices.DllImport("ole32.dll")]
		private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable prot);

		/// <summary>
		/// Get the currently available COM objects from the Running Objects Table
		/// </summary>
		/// <returns>A Hashtable mapping the object's display name to the object itself</returns>
		private static Hashtable GetRunningObjectTable()
		{
			var result = new Hashtable();

			var fetched = IntPtr.Zero;
			IRunningObjectTable runningObjectTable;
			IEnumMoniker monikerEnumerator;
			var monikers = new IMoniker[1];

			GetRunningObjectTable(0, out runningObjectTable);
			runningObjectTable.EnumRunning(out monikerEnumerator);
			monikerEnumerator.Reset();

			while (monikerEnumerator.Next(1, monikers, fetched) == 0)
			{
				IBindCtx ctx;
				CreateBindCtx(0, out ctx);

				string runningObjectName;
				monikers[0].GetDisplayName(ctx, null, out runningObjectName);

				object runningObjectVal;
				runningObjectTable.GetObject(monikers[0], out runningObjectVal);

				result[runningObjectName] = runningObjectVal;
			}

			return result;
		}

		/// <summary>
		/// Get the MapInfo COM object associated with the given window handle
		/// </summary>
		/// <returns>A MapInfoApplication wrapper for the requested window</returns>
		public static dynamic GetMapInfoInstance(string progId)
		{
			var runningObjects = GetRunningObjectTable();

			var rotEnumerator = runningObjects.GetEnumerator();
			while (rotEnumerator.MoveNext())
			{
				var candidateName = (string) rotEnumerator.Key;
				if (candidateName != null && !candidateName.Contains(progId))
				{
					continue;
				}

				//TODO: only return is Pro is Visible
				return rotEnumerator.Value;
			}

			return null;
		}

		#endregion COM Stuff

		public static string DefaultRegistryLocation = @"SOFTWARE\MapInfo\MapInfo\Professional\{0}";

		public static bool IsCmdLineArg(string arg, params string[] options)
		{
			return options.Aggregate(false, (current, option) => current || MatchesAny(arg, "-" + option, "/" + option));
		}

		public static bool MatchesAny(string arg, params string[] options)
		{
			return arg != null && options != null &&
			       options.Any(p => string.Compare(p, arg, StringComparison.OrdinalIgnoreCase) == 0);
		}

		static string FindProExe(string path)
		{
			if (File.Exists(path))
			{
				return path;
			}

			string[] versions = {"1900", "1700", "1600", "1520"};

			foreach (var ver in versions)
			{
				var keyString = string.Format(DefaultRegistryLocation, ver);
				var key = Registry.LocalMachine.OpenSubKey(keyString);
				if (key != null)
				{
					path = Path.Combine(key.GetValue("ProgramDirectory")?.ToString(), "MapInfoPro.exe");
					key.Close();
					if (File.Exists(path))
					{
						return path;
					}
				}
			}

			return null;
		}

		static bool LaunchProgram(string programId, string pathToExe, string fileToLaunch)
		{
			dynamic pro = GetMapInfoInstance(programId);

			// not running so lets start it
			if (pro != null)
			{
				try
				{
					pro.GetType().InvokeMember("Do", System.Reflection.BindingFlags.InvokeMethod | BindingFlags.Instance, null,
						pro,
						new object[1]
						{
							$"run application \"{fileToLaunch}\""
						});

				}
				catch (Exception e)
				{
					Console.WriteLine(e.InnerException?.Message);
					return false;
				}
			}
			else // pro is not running so lets launch it with file to run
			{
				var proExe = FindProExe(pathToExe);
				if (!string.IsNullOrWhiteSpace(proExe))
				{
					Process.Start(proExe, fileToLaunch);
				}
			}

			return true;
		}

		static void Usage()
		{
			Console.WriteLine(
				$"Usage: MapBasicHelper.exe [build [PathToMapBasic.exe] FolderToBuild] | [run [ProgramId] [PathToMapInfoPro.exe] [FileToRun | Folder]]");
		}

		// If there is a project file compile mb files from module list into mbo then link project
		// If no project file then we just compile the mb files in the passed in folder
		static int Main(string[] args)
		{
			// Validate arguments and show usage message
			if (args.Length < 2 || !MatchesAny(args[0], new[] {"build", "run"}))
			{
				Usage();
				return 1;
			}

			var compiler = new CompileMb();
			bool build = args[0].ToLower() == "build";
			var progId = "MapInfo.Application.x64";
			var pathToPro = string.Empty;
			var fileToRun = string.Empty;
			foreach (var arg in args)
			{
				if (arg == args[0]) continue;
				if (build)
				{
					compiler.FindMapBasic();
					if (arg.EndsWith("MapBasic.exe", StringComparison.OrdinalIgnoreCase))
					{
						compiler.MapBasicExe = arg;
					}
					else
					{
						compiler.OutputFolder = arg;
					}
				}

				if (!build)
				{
					if (arg.EndsWith("MapInfoPro.exe", StringComparison.OrdinalIgnoreCase))
					{
						pathToPro = arg;
					}
					else if (arg.EndsWith(".x64", StringComparison.OrdinalIgnoreCase))
					{
						progId = arg;
					}
					else
					{
						if (Directory.Exists(arg))
						{
							fileToRun = FindFileToRun(arg, "*.mbx");
							if (string.IsNullOrEmpty(fileToRun))
							{
								fileToRun = FindFileToRun(arg, "*.py");
							}
							if (string.IsNullOrEmpty(fileToRun))
							{
								fileToRun = FindFileToRun(arg, "*.wor");
							}
							if (string.IsNullOrEmpty(fileToRun))
							{
								Console.WriteLine($"No files (.mbx, .py, .wor) to run found in folder {arg}");
								Usage();
								return 1;
							}
						}
						else if (File.Exists(arg))
						{
							fileToRun = arg;
						}
						else
						{
							Console.WriteLine($"{arg} is not an existing file or folder");
							Usage();
							return 1;
						}
					}
				}
			}

			if (build)
			{
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

			if (string.IsNullOrWhiteSpace(fileToRun))
			{
				Usage();
				return 1;
			}

			return LaunchProgram(progId, pathToPro, fileToRun) ? 0 : 1;
		}

		static string FindFileToRun(string folder, string pattern)
		{
			var name = Path.GetFileName(folder);

			// look for an mbx or a .py file or a .wor, if more than one then prefer one with same base name as folder
			var files = Directory.EnumerateFiles(folder, pattern).ToArray();
			if (files.Length == 1)
			{
				return files[0];
			}

			if (files.Length > 1)
			{
				foreach (var file in files)
				{
					if (string.Compare(name, Path.GetFileNameWithoutExtension(file), StringComparison.CurrentCultureIgnoreCase) ==0)
					{
						return file;
					}
				}

				return files[0]; // pick first one if none found with preferred name
			}

			return null;
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
