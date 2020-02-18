using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace BuildMapBasicProject
{
	public static class ListExtension
	{
		public static bool AddIfNotExist<T>(this List<T> id, T value)
		{
			var item = id.FirstOrDefault(x => x.GetHashCode() == value.GetHashCode());
			if (Equals(item, default(T)))
			{
				id.Add(value);
				return true;
			}
			return false;
		}
	}

	public class IniFile
	{
		private readonly Dictionary<string, IniSection> _mSections;

		public IniFile(string sFileName)
		{
			_mSections = new Dictionary<string, IniSection>(StringComparer.CurrentCultureIgnoreCase);
			Load(sFileName);
		}

		private void Load(string sFileName)
		{
			RemoveAllSections();
			int lineNumber = 0;
			IniSection tempsection = null;
			StreamReader oReader = new StreamReader(sFileName);
			Regex regexsection = new Regex("^[\\s]*\\[[\\s]*([^\\[\\s].*[^\\s\\]])[\\s]*\\][\\s]*$", (RegexOptions.Singleline | RegexOptions.IgnoreCase));
			Regex regexkey = new Regex("^\\s*([^=\\s]*)[^=]*=(.*)", (RegexOptions.Singleline | RegexOptions.IgnoreCase));
			while (!oReader.EndOfStream)
			{
				string line = oReader.ReadLine();
				lineNumber++;
				if (!string.IsNullOrEmpty(line))
				{
					Match m;
					if ((m = regexsection.Match(line)).Success)
					{
						tempsection = AddSection(m.Groups[1].Value);
					}
					else if ((m = regexkey.Match(line)).Success && tempsection != null)
					{
						tempsection.AddKey(m.Groups[1].Value).AddValue(m.Groups[2].Value);
					}
					else if (tempsection != null)
					{
						tempsection.AddKey(line);
					}
				}
			}
			oReader.Close();
		}


		public void Save(string sFileName)
		{
			StreamWriter oWriter = new StreamWriter(sFileName, false);
			foreach (IniSection s in Sections)
			{
				oWriter.WriteLine("[{0}]", s.Name);
				foreach (IniKey k in s.Keys)
				{
					foreach (var value in k.Value)
					{
						oWriter.WriteLine("{0}={1}", k.Name, value);
					}
				}
			}
			oWriter.Close();
		}

		public System.Collections.ICollection Sections
		{
			get
			{
				return _mSections.Values;
			}
		}


		public IniSection AddSection(string sSection)
		{
			IniSection s;
			sSection = sSection.Trim();
			// Trim spaces
			if (_mSections.ContainsKey(sSection))
			{
				s = _mSections[sSection];
			}
			else
			{
				s = new IniSection(sSection);
				_mSections[sSection] = s;
			}
			return s;
		}
		public bool RemoveSection(string sSection)
		{
			sSection = sSection.Trim();
			return RemoveSection(this[sSection]);
		}
		public bool RemoveSection(IniSection section)
		{
			if (section != null)
			{
				try
				{
					_mSections.Remove(section.Name);
					return true;
				}
				catch (Exception ex)
				{
					Trace.WriteLine(ex.Message);
				}
			}
			return false;
		}
		public bool RemoveAllSections()
		{
			_mSections.Clear();
			return (_mSections.Count == 0);
		}

		public IniSection this[string sSection]
		{
			get
			{
				sSection = sSection.Trim();
				// Trim spaces
				if (_mSections.ContainsKey(sSection))
				{
					return _mSections[sSection];
				}
				return null;
			}
		}

	}

	public class IniSection
	{
		private readonly string _mSSection;
		private readonly Dictionary<string, IniKey> _mKeys;


		protected internal IniSection(string sSection)
		{
			_mSSection = sSection;
			_mKeys = new Dictionary<string, IniKey>(StringComparer.CurrentCultureIgnoreCase);
		}

		public System.Collections.ICollection Keys
		{
			get
			{
				return _mKeys.Values;
			}
		}

		public string Name
		{
			get
			{
				return _mSSection;
			}
		}

		public IniKey AddKey(string sKey)
		{
			sKey = sKey.Trim();
			IniKey k = null;
			if (sKey.Length != 0)
			{
				if (!_mKeys.ContainsKey(sKey))
				{
					k = new IniKey(sKey);
					_mKeys[sKey] = k;
				}
				else
					k = _mKeys[sKey];
			}
			return k;
		}
		public bool RemoveKey(string sKey)
		{
			return RemoveKey(this[sKey]);
		}
		public bool RemoveKey(IniKey key)
		{
			if (key != null)
			{
				try
				{
					_mKeys.Remove(key.Name);
					return true;
				}
				catch (Exception ex)
				{
					Trace.WriteLine(ex.Message);
				}
			}
			return false;
		}
		public bool RemoveAllKeys()
		{
			_mKeys.Clear();
			return (_mKeys.Count == 0);
		}

		public IniKey this[string sKey]
		{
			get
			{
				sKey = sKey.Trim();
				if (_mKeys.ContainsKey(sKey))
				{
					return _mKeys[sKey];
				}
				return null;
			}
		}

		public bool RemoveValue(string sKey, int index = 0)
		{
			if (this[sKey] != null)
			{
				return this[sKey].RemoveValue(index);
			}
			return false;
		}

		public void RemoveAllValues(string sKey)
		{
			if (this[sKey] != null)
			{
				this[sKey].Value.Clear();
			}
		}
	}

	public class IniKey
	{
		private readonly string _mSKey;
		private List<string> _mSValue;

		protected internal IniKey(string sKey)
		{
			_mSKey = sKey;
			_mSValue = new List<string>();
		}

		public string Name
		{
			get
			{
				return _mSKey;
			}
		}

		public List<string> Value
		{
			get
			{
				return _mSValue;
			}
			set
			{
				_mSValue = value;
			}
		}

		public string this[int index]
		{
			get
			{
				try
				{
					return _mSValue[index];
				}
				catch (ArgumentOutOfRangeException)
				{
					return null;
				}
			}
			set
			{
				try
				{
					_mSValue[index] = value;
				}
				catch (ArgumentOutOfRangeException)
				{
					// Do nothing...
				}
			}
		}
		public void AddValue(string sValue)
		{
			_mSValue.AddIfNotExist(sValue);
		}

		public bool RemoveValue(int index)
		{
			try
			{
				Value.RemoveAt(index);
				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}
	}
}