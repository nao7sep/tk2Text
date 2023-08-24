using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nekote;

namespace tk2Text
{
    internal struct iAttributesInfo
    {
        public string SourceDirectoryPath;

        public string CategoryName;

        public string DestDirectoryPath;

        public string AttachedFileDirectoryRelativePath;

        private string? mAttachedFileDirectoryPath = null;

        public string AttachedFileDirectoryPath
        {
            get
            {
                if (mAttachedFileDirectoryPath == null)
                    mAttachedFileDirectoryPath = nPath.Combine (DestDirectoryPath, AttachedFileDirectoryRelativePath);

                return mAttachedFileDirectoryPath;
            }
        }

        public string DestFileName;

        private string? mDestFilePath = null;

        public string DestFilePath
        {
            get
            {
                if (mDestFilePath == null)
                    mDestFilePath = nPath.Combine (DestDirectoryPath, DestFileName);

                return mDestFilePath;
            }
        }

        public string Title;

        public iAttributesInfo (string sourceDirectoryPath, string categoryName, string destDirectoryPath, string attachedFileDirectoryRelativePath, string destFileName, string title)
        {
            SourceDirectoryPath = sourceDirectoryPath;
            CategoryName = categoryName;
            DestDirectoryPath = destDirectoryPath;
            AttachedFileDirectoryRelativePath = iShared.ToWindowsDirectorySeparators (attachedFileDirectoryRelativePath);
            DestFileName = destFileName;
            Title = title;
        }

        // readonly をつけないと、IDE0251 のメッセージが表示される
        // 説明がなく、リンクをクリックすると、2023年7月2日の時点では404になる
        // なくてもよさそうなものだが、IDE を黙らせるためにつけておく

        // readonly の注意点 - C# によるプログラミング入門 | ++C++; // 未確認飛行 C
        // https://ufcpp.net/study/csharp/resource/readonlyness/#readonly-member

        public readonly string ToString (bool includesSourceDirectoryPath)
        {
            return $"{(includesSourceDirectoryPath ? $"{SourceDirectoryPath} | " : string.Empty)}{CategoryName} | {DestDirectoryPath} | {AttachedFileDirectoryRelativePath} | {DestFileName} | {Title}";
        }

        public static readonly iAttributesInfo Empty = new iAttributesInfo
        {
            SourceDirectoryPath = string.Empty,
            CategoryName = string.Empty,
            DestDirectoryPath = string.Empty,
            AttachedFileDirectoryRelativePath = string.Empty,
            DestFileName = string.Empty,
            Title = string.Empty
        };
    }
}
