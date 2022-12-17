using System;

namespace SSLCertificateMaker
{
    public partial class ConvertCerts
    {
        private class CertConversionHandler
		{
			public Func<string, CertificateBundle> _readInput;
			public Action<string, CertificateBundle> _writeOutput;
			public string[] _fileExtensions;

			public CertConversionHandler(Func<string, CertificateBundle> ReadInput, Action<string, CertificateBundle> WriteOutput, params string[] fileExtensions)
			{
				_readInput = ReadInput;
				_writeOutput = WriteOutput;
				_fileExtensions = fileExtensions;
			}

			public bool IsAllowedSource(string path)
			{
				foreach (string extension in _fileExtensions)
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
