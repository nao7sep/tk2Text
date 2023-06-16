using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tk2Text
{
    public class TaskOrderComparer: IComparer <TaskInfo>
    {
        public int Compare (TaskInfo? x, TaskInfo? y)
        {
            if (x == null)
            {
                if (y == null)
                    return 0;

                else return -1;
            }

            else
            {
                if (y == null)
                    return 1;

                else
                {
                    if (x.HandlingUtc != null)
                    {
                        if (y.HandlingUtc != null)
                        {
                            if (x.HandlingUtc < y.HandlingUtc)
                                return -1;

                            else if (x.HandlingUtc == y.HandlingUtc)
                                return 0;

                            else return 1;
                        }

                        else return 1;
                    }

                    else
                    {
                        if (y.HandlingUtc != null)
                            return -1;

                        else
                        {
                            // 他リストからインポートされたタスクだと OrderingUtc が -1 に、
                            //     テキストファイルから読み込まれたものだと -2 から下っていく値になる

                            // 例:
                            //     インポートされたタスク #2 (-1)
                            //     インポートされたタスク #1 (-1)
                            //     ロードされたタスク #1 (-2)
                            //     ロードされたタスク #2 (-3)
                            //     通常のタスク #1 (100)
                            //     通常のタスク #2 (101)

                            // インポートされたタスクの順序は不定
                            // ロードされたタスクは、OrderingUtc で降順に
                            // 通常のタスクは、OrderingUtc で昇順に
                            // ここでは仮に100からとしたが、Ticks なので long の大きい値に

                            // 変なソートなのは、元々、ロードされたタスクの順序は不定で考えていたため
                            // 順序を考えて作ったリストでもロード時にシャッフルされるのが不便で、あとからソートを実装した
                            // インポートされたタスクをロードされたタスクより先に表示したく、
                            //     0以上か未満かにグループ分けし、それぞれの並び替えに整合性を与えた

                            if (x.OrderingUtc >= 0)
                            {
                                if (y.OrderingUtc >= 0)
                                {
                                    if (x.OrderingUtc < y.OrderingUtc)
                                        return -1;

                                    else if (x.OrderingUtc == y.OrderingUtc)
                                        return 0;

                                    else return 1;
                                }

                                else return 1;
                            }

                            else
                            {
                                if (y.OrderingUtc >= 0)
                                    return -1;

                                else
                                {
                                    if (x.OrderingUtc < y.OrderingUtc)
                                        return 1;

                                    else if (x.OrderingUtc == y.OrderingUtc)
                                        return 0;

                                    else return -1;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
