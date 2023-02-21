using System.Collections.Generic;

namespace FIM.MARE
{
    public class TrimEnd : Transform
    {
        public override object Convert(object value)
        {
            if (value.GetType() == typeof(string))
            {
                return string.IsNullOrEmpty(value as string) ? value : value.ToString().TrimEnd();
            }
            else if (value is System.Collections.IEnumerable)
            {
                List<object> result = new List<object>();
                foreach (object o in (System.Collections.IEnumerable)value)
                {
                    result.Add(o.ToString().TrimEnd());
                }
                return result;
            }
            else
            {
                return value;
            }
        }
    }
}
