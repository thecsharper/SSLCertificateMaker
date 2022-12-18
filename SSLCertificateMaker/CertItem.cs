namespace SSLCertificateMaker
{
    public partial class ConvertCerts
    {
        private class CertItem
		{
			public string Name;
			public string FullName;

			public CertItem(string name, string fullName)
			{
				Name = name;
				FullName = fullName;
			}

			public override string ToString()
			{
				return Name;
			}
		}
	}
}
