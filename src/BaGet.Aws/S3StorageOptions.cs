using System.ComponentModel.DataAnnotations;
using BaGet.Core;

namespace BaGet.Aws
{
    public class S3StorageOptions
    {
        [RequiredIf(nameof(SecretKey), null, IsInverted = true)]
        public string AccessKey { get; set; }

        [RequiredIf(nameof(AccessKey), null, IsInverted = true)]
        public string SecretKey { get; set; }

        [Required]
        public string Region { get; set; }

        [Required]
        public string Bucket { get; set; }

        public string Prefix { get; set; }

        public bool UseInstanceProfile { get; set; }

        public string AssumeRoleArn { get; set; }
        public bool ForcePathStyle { get; set; }

        public string ServiceUrl { get; set; }

        public bool UseHttp { get; set; }

        public int Timeout { get; set; } = 300;
    }
}
