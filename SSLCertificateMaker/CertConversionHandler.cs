using System;

namespace SSLCertificateMaker
{
    public partial class ConvertCerts
    {
        private class CertConversionHandler
		{
			public Func<string, CertificateBundle> ReadInput;
			public Action<string, CertificateBundle> WriteOutput;
			public string[] fileExtensions;

			public CertConversionHandler(Func<string, CertificateBundle> ReadInput, Action<string, CertificateBundle> WriteOutput, params string[] fileExtensions)
			{
				this.ReadInput = ReadInput;
				this.WriteOutput = WriteOutput;
				this.fileExtensions = fileExtensions;
			}
			public bool IsAllowedSource(string path)
			{
				foreach (string extension in fileExtensions)
				{
					if (path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
					{
                        return true;
                    }
				}

				return false;
			}
		}
	}
}
