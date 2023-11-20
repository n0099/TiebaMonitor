#pragma warning disable SA1210 // Using directives should be ordered alphabetically by namespace
global using System.ComponentModel.DataAnnotations;
global using System.Text.Json;
global using System.Threading.Channels;
global using System.Threading.RateLimiting;

global using Autofac;
global using CommunityToolkit.Diagnostics;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;
global using OpenCvSharp;
global using LanguageExt;
global using Polly;
global using Polly.Extensions.Http;
global using Polly.Registry;
global using SixLabors.ImageSharp.Formats.Bmp;
global using SixLabors.ImageSharp.Formats.Gif;
global using SixLabors.ImageSharp.Formats.Jpeg;
global using SixLabors.ImageSharp.Formats.Png;
global using SuperLinq;

global using tbm.ImagePipeline.Consumer;
global using tbm.ImagePipeline.Db;
global using tbm.ImagePipeline.Ocr;
global using tbm.Shared;

global using Fid = System.UInt32;
global using ImageId = System.UInt32;
