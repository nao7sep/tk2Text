using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tk2Text
{
    // パスのリストを GroupBy により「一つ以上のディレクトリーが統合されたタスクリスト」の集まりにするためのもの

    internal class iMergedPathsEqualityComparer: IEqualityComparer <string>
    {
        public readonly IEnumerable <iMergedPathsInfo> MergedPaths;

        public iMergedPathsEqualityComparer (IEnumerable <iMergedPathsInfo> mergedPaths)
        {
            MergedPaths = mergedPaths;
        }

        public bool Equals (string? left, string? right)
        {
            return MergedPaths.Any (x => string.Equals (x.LeftPath, left, StringComparison.OrdinalIgnoreCase) &&
                string.Equals (x.RightPath, right, StringComparison.OrdinalIgnoreCase));
        }

        public int GetHashCode (string value)
        {
            // まずハッシュを比較し、それが一致する場合のみ Equals でメインの比較
            // value.GetHashCode を返すと Equals に到達せず false になる
            return 0;
        }
    }
}
