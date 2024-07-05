using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shap.Flexer
{
    public interface IItemClass<ItemType>
        where ItemType : notnull
    {
        public bool Contains(ItemType item);
    }

    public class ItemClassEquator<ItemType>(ItemType value) : IItemClass<ItemType>
        where ItemType : notnull
    {
        readonly ItemType value = value;

        public bool Contains(ItemType item)
        {
            return value.Equals(item);
        }
    }

    public class ItemClassAlwaysTrue<ItemType> : IItemClass<ItemType>
        where ItemType : notnull
    {
        public bool Contains(ItemType item) => true;
    }

    public class TokenTypeComparer<TokenType>(TokenType tokenType) : IItemClass<StringFlexer<TokenType>.Token>
        where TokenType : Enum
    {
        readonly TokenType tokenType = tokenType;

        public bool Contains(StringFlexer<TokenType>.Token token)
        {
            return token.type.Equals(tokenType);
        }
    }
}
