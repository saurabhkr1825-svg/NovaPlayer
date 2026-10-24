# Third-Party Licenses and Redistribution Notices

NovaPlayer incorporates open-source software libraries. This document provides required copyright notices, permission notices, and distribution conditions for third-party components included in or distributed with NovaPlayer.

---

## Table of Third-Party Dependencies

| Component | Version | License | Copyright / Author | Source / Upstream Repository |
| :--- | :--- | :--- | :--- | :--- |
| **LibVLC (Native)** | 3.0.21 | LGPL-2.1-or-later | VideoLAN and VLC authors | [VideoLAN Git](https://code.videolan.org/videolan/vlc) |
| **LibVLCSharp** | 3.9.2 | LGPL-2.1-or-later | VideoLAN and LibVLCSharp authors | [LibVLCSharp Git](https://code.videolan.org/videolan/LibVLCSharp) |
| **LibVLCSharp.WPF** | 3.9.2 | LGPL-2.1-or-later | VideoLAN and LibVLCSharp authors | [LibVLCSharp Git](https://code.videolan.org/videolan/LibVLCSharp) |
| **Microsoft.Data.Sqlite** | 8.0.10 | MIT License | Microsoft Corporation | [dotnet/efcore](https://github.com/dotnet/efcore) |
| **Microsoft.Extensions.DependencyInjection** | 8.0.1 | MIT License | Microsoft Corporation | [dotnet/runtime](https://github.com/dotnet/runtime) |
| **CommunityToolkit.Mvvm** | 8.3.2 | MIT License | .NET Foundation and Contributors | [CommunityToolkit](https://github.com/CommunityToolkit/dotnet) |
| **SQLitePCLRaw** | 2.1.10 | Apache-2.0 | Eric Sink / SQLitePCLRaw authors | [SQLitePCL.raw](https://github.com/ericsink/SQLitePCL.raw) |
| **SQLite (Native Engine)** | 3.46.1 | Public Domain | D. Richard Hipp / SQLite Team | [sqlite.org](https://www.sqlite.org/) |
| **Serilog** | 4.1.0 | Apache-2.0 | Serilog Contributors | [serilog/serilog](https://github.com/serilog/serilog) |
| **Serilog.Sinks.File** | 6.0.0 | Apache-2.0 | Serilog Contributors | [serilog/serilog-sinks-file](https://github.com/serilog/serilog-sinks-file) |
| **Serilog.Sinks.Console** | 6.0.0 | Apache-2.0 | Serilog Contributors | [serilog/serilog-sinks-console](https://github.com/serilog/serilog-sinks-console) |

---

## LibVLC Compliance & Redistribution Obligations (LGPL-2.1)

NovaPlayer utilizes the native **LibVLC** library and **LibVLCSharp** bindings under the terms of the **GNU Lesser General Public License, Version 2.1 (LGPL-2.1)**.

In compliance with LGPL-2.1 Section 6:
1. **Dynamic Linking**: NovaPlayer links to LibVLC dynamically at runtime (`libvlc.dll` and `libvlccore.dll` via P/Invoke interop). NovaPlayer's proprietary/application code is not a derivative work of LibVLC and is distributed under its own project license.
2. **User Modification and Replacement**: Users have the explicit right and technical capability to replace, upgrade, or modify the LibVLC shared libraries and plugins bundled in the application's `libvlc\win-x64\` installation directory with compatible custom-built or upstream LibVLC binaries.
3. **Source Code Availability**: Unmodified corresponding source code for the bundled LibVLC 3.0.21 release can be freely downloaded from:
   - Official VideoLAN Sources: https://www.videolan.org/vlc/download-sources.html
   - VideoLAN GitLab Repository: https://code.videolan.org/videolan/vlc/-/tree/3.0.21
   - LibVLCSharp Sources: https://code.videolan.org/videolan/LibVLCSharp/-/tree/3.9.2
4. **Trademark Notice**: "LibVLC" and the VLC cone logo are registered trademarks of VideoLAN. NovaPlayer is an independent project and is neither endorsed by nor affiliated with VideoLAN.

---

## Full License Texts

### 1. GNU Lesser General Public License (LGPL), Version 2.1
```text
                  GNU LESSER GENERAL PUBLIC LICENSE
                       Version 2.1, February 1999

 Copyright (C) 1991, 1999 Free Software Foundation, Inc.
 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA
 Everyone is permitted to copy and distribute verbatim copies
 of this license document, but changing it is not allowed.

[This is the first released version of the Lesser GPL.  It also counts
 as the successor of the GNU Library Public License, version 2, hence
 the version number 2.1.]

                            Preamble

  The licenses for most software are designed to take away your
freedom to share and change it.  By contrast, the GNU General Public
Licenses are intended to guarantee your freedom to share and change
free software--to make sure the software is free for all its users.

  This license, the Lesser General Public License, applies to some
specially designated software packages--typically libraries--of the
Free Software Foundation and other authors who decide to use it.  You
can use it too, but we suggest you first think carefully about whether
this license or the ordinary General Public License is the better
strategy to use in any particular case, based on the explanations below.

  When we speak of free software, we are referring to freedom of use,
not price.  Our General Public Licenses are designed to make sure that
you have the freedom to distribute copies of free software (and charge
for this service if you wish); that you receive source code or can get
it if you want it; that you can change the software and use pieces of
it in new free programs; and that you are informed that you can do
these things.

  To protect your rights, we need to make restrictions that forbid
distributors to deny you these rights or to ask you to surrender these
rights.  These restrictions translate to certain responsibilities for
you if you distribute copies of the library or if you modify it.

  For example, if you distribute copies of the library, whether gratis
or for a fee, you must give the recipients all the rights that we gave
you.  You must make sure that they, too, receive or can get the source
code.  If you link other code with the library, you must provide
complete object files to the recipients, so that they can relink them
with the library after making changes to the library and recompiling
it.  And you must show them these terms so they know their rights.

  We protect your rights with a two-step method: (1) we copyright the
library, and (2) we offer you this license, which gives you legal
permission to copy, distribute and/or modify the library.

  To protect each distributor, we want to make it very clear that
there is no warranty for the free library.  Also, if the library is
modified by someone else and passed on, the recipients should know
that what they have is not the original version, so that the original
author's reputation will not be affected by problems that might be
introduced by others.

  Finally, software patents pose a constant threat to the existence of
any free program.  We wish to make sure that a company cannot
effectively restrict the users of a free program by obtaining a
restrictive license from a patent holder.  Therefore, we insist that
any patent license obtained for a version of the library must be
consistent with the full freedom of use specified in this license.

  Most GNU software, including some libraries, is covered by the
ordinary GNU General Public License.  This license, the GNU Lesser
General Public License, applies to certain designated libraries, and
is quite different from the ordinary General Public License.  We use
this license for certain libraries in order to permit linking those
libraries into non-free programs.

  When a program is linked with a library, whether statically or using
a shared library, the combination of the two is legally speaking a
combined work, a derivative of the original library.  The ordinary
General Public License therefore permits such linking only if the
entire combination fits its criteria of freedom.  The Lesser General
Public License permits more lax criteria for linking other code with
the library.

  We call this license the "Lesser" General Public License because it
does Less to protect the user's freedom than the ordinary General
Public License.  It also provides other free software developers Less
of an advantage over competing non-free programs.  These disadvantages
are the reason we use the ordinary General Public License for many
libraries.  However, the Lesser license provides advantages in certain
special circumstances.

  For example, on rare occasions, there may be a special need to
encourage the widest possible use of a certain library, so that it becomes
a de-facto standard.  To achieve this, non-free programs must be
allowed to use the library.  A more frequent case is that a free
library does the same job as widely used non-free libraries.  In this
case, there is little to gain by limiting the free library to free
software only, so we use the Lesser General Public License.

  In other cases, permission to use a particular library in non-free
programs enables a greater number of people to use a large body of
free software.  For example, permission to use the GNU C Library in
non-free programs enables many more people to use the whole GNU
operating system, as well as its variant, the GNU/Linux operating
system.

  Although the Lesser General Public License is Less protective of the
users' freedom, it does ensure that the user of a program that is linked
with the Library has the freedom and the wherewithal to run that program
using a modified version of the Library.

  The precise terms and conditions for copying, distribution and
modification follow.  Pay close attention to the difference between a
"work based on the library" and a "work that uses the library".  The
former contains code derived from the library, whereas the latter must
be combined with the library in order to run.
```

---

### 2. MIT License
Applies to: **Microsoft.Data.Sqlite**, **Microsoft.Extensions.DependencyInjection**, **CommunityToolkit.Mvvm**

```text
The MIT License (MIT)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

### 3. Apache License, Version 2.0
Applies to: **Serilog**, **Serilog.Sinks.File**, **Serilog.Sinks.Console**, **SQLitePCLRaw**

```text
                              Apache License
                        Version 2.0, January 2004
                     http://www.apache.org/licenses/

TERMS AND CONDITIONS FOR USE, REPRODUCTION, AND DISTRIBUTION

1. Definitions.
   "License" shall mean the terms and conditions for use, reproduction,
   and distribution as defined by Sections 1 through 9 of this document.
   "Licensor" shall mean the copyright owner or entity authorized by
   the copyright owner that is granting the License.
   "Legal Entity" shall mean the union of the acting entity and all
   other entities that control, are controlled by, or are under common
   control with that entity.
   "You" (or "Your") shall mean an individual or Legal Entity exercising
   permissions granted by this License.
   "Source" form shall mean the preferred form for making modifications,
   including but not limited to software source code, documentation source,
   and configuration files.
   "Object" form shall mean any form resulting from mechanical transformation
   or translation of a Source form, including but not limited to compiled
   object code, generated documentation, and conversions to other media types.
   "Work" shall mean the work of authorship, whether in Source or Object form,
   made available under the License.
   "Derivative Works" shall mean any work, whether in Source or Object form,
   that is based on (or derived from) the Work and for which the editorial
   revisions, annotations, elaborations, or other modifications represent,
   as a whole, an original work of authorship.
   "Contribution" shall mean any work of authorship, including the original
   version of the Work and any modifications or additions to that Work
   or Derivative Works thereof, that is intentionally submitted to Licensor.

2. Grant of Copyright License. Subject to the terms and conditions of this
   License, each Contributor hereby grants to You a perpetual, worldwide,
   non-exclusive, no-charge, royalty-free, irrevocable copyright license to
   reproduce, prepare Derivative Works of, publicly display, publicly perform,
   sublicense, and distribute the Work and such Derivative Works in Source
   or Object form.

3. Grant of Patent License. Subject to the terms and conditions of this
   License, each Contributor hereby grants to You a perpetual, worldwide,
   non-exclusive, no-charge, royalty-free, irrevocable patent license to
   make, have made, use, offer to sell, sell, import, and otherwise transfer
   the Work.

4. Redistribution. You may reproduce and distribute copies of the Work or
   Derivative Works thereof in any medium, with or without modifications,
   and in Source or Object form, provided that You meet the following
   conditions:
   (a) You must give any other recipients of the Work or Derivative Works
       a copy of this License; and
   (b) You must cause any modified files to carry prominent notices stating
       that You changed the files; and
   (c) You must retain, in the Source form of any Derivative Works that You
       distribute, all copyright, patent, trademark, and attribution notices;
   (d) If the Work includes a "NOTICE" text file as part of its distribution,
       then any Derivative Works that You distribute must include a readable
       copy of the attribution notices contained within such NOTICE file.

5. Submission of Contributions. Unless You explicitly state otherwise, any
   Contribution intentionally submitted for inclusion in the Work by You
   to the Licensor shall be under the terms and conditions of this License,
   without any additional terms or conditions.

6. Trademarks. This License does not grant permission to use the trade names,
   trademarks, service marks, or product names of the Licensor, except as
   required for reasonable and customary use in describing the origin of
   the Work and reproducing the content of the NOTICE file.

7. Disclaimer of Warranty. Unless required by applicable law or agreed to
   in writing, Licensor provides the Work (and each Contributor provides its
   Contributions) on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF
   ANY KIND, either express or implied, including, without limitation, any
   warranties or conditions of TITLE, NON-INFRINGEMENT, MERCHANTABILITY,
   or FITNESS FOR A PARTICULAR PURPOSE.

8. Limitation of Liability. In no event and under no legal theory, whether
   in tort (including negligence), contract, or otherwise, shall any
   Contributor be liable to You for damages, including any direct, indirect,
   special, incidental, or consequential damages.
```

---

### 4. SQLite Public Domain Dedication
Applies to: **SQLite Core Engine**

```text
The author disclaims copyright to this source code. In place of a legal notice,
here is a blessing:

   May you do good and not evil.
   May you find forgiveness for yourself and forgive others.
   May you share freely, never taking more than you give.
```
