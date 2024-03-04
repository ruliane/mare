using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace FIM.MARE
{
    public class MultiValueOr : Transform
    {
        public override object Convert(object value)
        {
            if (value == null) return value;

            List<object> values = FromValueCollection(value);
            List<object> returnValues = new List<object>();

            foreach (object val in values)
            {
                if (bool.Parse(val as string))
                {
                    returnValues.Add(true);
                    return returnValues;
                }
            }
            returnValues.Add(false);
            return returnValues;
        }
    }
}
