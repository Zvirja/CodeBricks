using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CodeBricks.Tests.Utils
{
    public class IdentityObject
    {
        public long Id { get; }

        public IdentityObject(long id)
        {
            Id = id;
        }

        public override string ToString() => Id.ToString(CultureInfo.InvariantCulture);
    }
}
