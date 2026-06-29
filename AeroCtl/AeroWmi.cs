using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AeroCtl;

/// <summary>
/// Wraps the Gigabyte WMI interface.
/// </summary>
public class AeroWmi : IDisposable
{
	#region Fields

	private readonly ManagementClass getClass;
	private readonly ManagementClass setClass;
	private readonly ManagementObject get;
	private readonly ManagementObject set;

	private bool initialized;

	#endregion

	#region Properties

	private string baseBoard;
	public string BaseBoard
	{
		get
		{
			this.ensureInitialized();
			return this.baseBoard;
		}
	}

	private string serialNumber;
	public string SerialNumber
	{
		get
		{
			this.ensureInitialized();
			return this.serialNumber;
		}
	}

	private string sku;
	public string Sku
	{
		get
		{
			this.ensureInitialized();
			return this.sku;
		}
	}

	private ImmutableArray<string> biosVersions;
	public ImmutableArray<string> BiosVersions
	{
		get
		{
			this.ensureInitialized();
			return this.biosVersions;
		}
	}

	#endregion

	public AeroWmi()
	{
		const string errorHint = "Either the Gigabyte WMI driver is missing (make sure Gigabyte Control Center is installed but not running) or your device model isn't supported.";

		ManagementScope scope = new ManagementScope("root\\WMI", new ConnectionOptions
		{
			EnablePrivileges = true,
			Impersonation = ImpersonationLevel.Impersonate
		});

		try
		{
			this.getClass = new ManagementClass(scope, new ManagementPath("GB_WMIACPI_Get"), null);
			this.get = this.getClass.GetInstances().OfType<ManagementObject>().FirstOrDefault();
		}
		catch (ManagementException ex)
		{
			throw new InvalidOperationException(
				new StringBuilder()
					.AppendLine("Failed to query GB_WMIACPI_Get.")
					.Append(errorHint)
					.ToString(),
				ex);
		}

		if (this.get == null)
			throw new InvalidOperationException(
				new StringBuilder()
					.AppendLine("Failed to find instance for GB_WMIACPI_Get.")
					.Append(errorHint)
					.ToString());

		try
		{
			this.setClass = new ManagementClass(scope, new ManagementPath("GB_WMIACPI_Set"), null);
			this.set = this.setClass.GetInstances().OfType<ManagementObject>().FirstOrDefault();
		}
		catch (ManagementException ex)
		{
			throw new InvalidOperationException(
				new StringBuilder()
					.AppendLine("Failed to query GB_WMIACPI_Set.")
					.Append(errorHint)
					.ToString(),
				ex);
		}

		if (this.set == null)
			throw new InvalidOperationException(
				new StringBuilder()
					.AppendLine("Failed to find instance for GB_WMIACPI_Set.")
					.Append(errorHint)
					.ToString());
	}

	private void ensureInitialized()
	{
		if (this.initialized)
			return;

		this.initialized = true;

		ManagementObject win32BaseBoard = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_BaseBoard")
			.Get()
			.OfType<ManagementObject>()
			.FirstOrDefault();

		ManagementObject win32Bios = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_BIOS")
			.Get()
			.OfType<ManagementObject>()
			.FirstOrDefault();

		ManagementObject win32ComputerSystem = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_ComputerSystem")
			.Get()
			.OfType<ManagementObject>()
			.FirstOrDefault();

		if (win32BaseBoard != null)
		{
			this.baseBoard = (string)win32BaseBoard["Product"];
		}

		if (win32Bios != null)
		{
			this.serialNumber = (string)win32Bios["SerialNumber"];
			this.biosVersions = [..((string[])win32Bios["BIOSVersion"])];
		}

		if (win32ComputerSystem != null)
		{
			this.sku = (string)win32ComputerSystem["SystemSKUNumber"];
		}
	}

	/// <summary>
	/// Invokes a WMI method.
	/// </summary>
	/// <param name="methodName"></param>
	/// <param name="parameters"></param>
	/// <returns></returns>
	public ImmutableDictionary<string, object> Invoke(string methodName, params (string, object)[] parameters)
	{
		try
		{
			ManagementObject target = this.get;
			MethodData m;

			try
			{
				m = this.getClass.Methods[methodName];
			}
			catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.NotFound)
			{
				target = this.set;
				m = this.setClass.Methods[methodName];
			}

			ManagementBaseObject inParams = m.InParameters;

			if (inParams != null)
			{
				foreach ((string name, object value) in parameters)
				{
					inParams[name] = value;
				}
			}
			else
			{
				if (parameters.Length != 0)
					throw new TargetParameterCountException($"Method does not take any parameters, but {parameters.Length} were supplied.");
			}

			ManagementBaseObject outParams = target.InvokeMethod(methodName, inParams, null);
			ImmutableDictionary<string, object> result = ImmutableDictionary<string, object>.Empty;

			if (outParams != null)
			{
				foreach (var res in outParams.Properties)
				{
					result = result.Add(res.Name, res.Value);
				}
			}

			Debug.WriteLine($"{methodName}({string.Join(", ", parameters.Select(p => $"{p.Item1} = {p.Item2}"))}) = {string.Join(", ", result.Select(p => $"{p.Key} = {p.Value}"))}");

			return result;
		}
		catch (ManagementException ex)
		{
			throw new ManagementException($"Failed to execute method '{methodName}': {ex.Message}", ex);
		}
	}

	/// <summary>
	/// Invokes a WMI method asynchronously.
	/// </summary>
	/// <param name="methodName"></param>
	/// <param name="parameters"></param>
	/// <returns></returns>
	public Task<ImmutableDictionary<string, object>> InvokeAsync(string methodName, params (string, object)[] parameters)
	{
		return Task.Run(() => this.Invoke(methodName, parameters));
	}

	public T InvokeSet<T>(string methodName, T value)
	{
		try
		{
			ManagementBaseObject inParams = this.set.GetMethodParameters(methodName);
			inParams["Data"] = value;
			ManagementBaseObject outParams = this.set.InvokeMethod(methodName, inParams, null);

			T res = default;
			if (outParams != null)
				res = (T)outParams["DataOut"];

			Debug.WriteLine($"{methodName}({value}) = {res}");
			return res;
		}
		catch (ManagementException ex)
		{
			throw new ManagementException($"Failed to execute setter method '{methodName}': {ex.Message}", ex);
		}
	}

	public Task<T> InvokeSetAsync<T>(string methodName, T value)
	{
		return Task.Run(() => this.InvokeSet(methodName, value));
	}

	public T InvokeGet<T>(string methodName)
	{
		try
		{
			ManagementBaseObject inParams = this.getClass.GetMethodParameters(methodName);

			if (methodName == "GetNvPowerConfig")
			{
				if (inParams != null)
				{
					try
					{
						inParams["Index"] = null;
					}
					catch (ManagementException)
					{
					}
				}
			}

			ManagementBaseObject outParams = this.get.InvokeMethod(methodName, inParams, null);
			T res = outParams == null ? default : (T)outParams["Data"];
			Debug.WriteLine($"{methodName}() = {res}");
			return res;
		}
		catch (ManagementException ex)
		{
			throw new ManagementException($"Failed to execute getter method '{methodName}': {ex.Message}", ex);
		}
	}

	public bool HasMethod(string methodName)
	{
		foreach (var method in this.getClass.Methods)
		{
			if (method.Name == methodName)
				return true;
		}

		Debug.WriteLine($"HasMethod('{methodName}'): method not found in GB_WMIACPI_Get (checked {this.getClass.Methods.Count} methods)");
		return false;
	}

	public Task<T> InvokeGetAsync<T>(string methodName)
	{
		return Task.Run(() => this.InvokeGet<T>(methodName));
	}

	public void Dispose()
	{
		this.get?.Dispose();
		this.getClass?.Dispose();

		this.set?.Dispose();
		this.setClass?.Dispose();
	}
}