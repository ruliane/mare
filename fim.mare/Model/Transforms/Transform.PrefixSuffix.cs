using System.Xml.Serialization;

namespace FIM.MARE
{
    public class PrefixSuffix : Transform
    {
        [XmlAttribute("Prefix")]
        public string Prefix { get; set; }
        [XmlAttribute("Suffix")]
        public string Suffix { get; set; }

        public override object Convert(object value)
        {
            Tracer.TraceInformation("transform-prefixsuffix {0} {1} {2}", Prefix, value as string, Suffix);
            return string.Concat(Prefix, value as string, Suffix);
        }
    }
}
