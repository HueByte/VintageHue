global using Xunit;
global using FluentAssertions;
global using Moq;
global using System;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Collections.Generic;
global using System.Linq;
global using System.IO;

// Conditional imports for mod classes
global using HueHordes.AI;
global using HueHordes.Models;
global using HueHordes.Behaviors;

#if VINTAGE_STORY_AVAILABLE
global using Vintagestory.API.Server;
global using Vintagestory.API.Common;
global using Vintagestory.API.MathTools;
#endif