﻿namespace RaspberryPiDotnetRepository.Data.ControlMetadata;

/*
 * To regenerate:
 * 1. Go to https://packages.debian.org/bookworm/
 * 2. Run Array.from(document.querySelectorAll("#content dt a")).map(a => a.getAttribute("href").replace(/\/$/, '').replace('-', '_').toUpperCase()).join(",\n")
 * 3. Copy the output string here
 */
public enum Section {

    ADMIN,
    CLI_MONO,
    COMM,
    DATABASE,
    DEBIAN_INSTALLER,
    DEBUG,
    DEVEL,
    DOC,
    EDITORS,
    EDUCATION,
    ELECTRONICS,
    EMBEDDED,
    FONTS,
    GAMES,
    GNOME,
    GNU_R,
    GNUSTEP,
    GOLANG,
    GRAPHICS,
    HAMRADIO,
    HASKELL,
    HTTPD,
    INTERPRETERS,
    INTROSPECTION,
    JAVA,
    JAVASCRIPT,
    KDE,
    KERNEL,
    LIBDEVEL,
    LIBS,
    LISP,
    LOCALIZATION,
    MAIL,
    MATH,
    METAPACKAGES,
    MISC,
    NET,
    NEWS,
    OCAML,
    OLDLIBS,
    OTHEROSFS,
    PERL,
    PHP,
    PYTHON,
    RUBY,
    RUST,
    SCIENCE,
    SHELLS,
    SOUND,
    TASKS,
    TEX,
    TEXT,
    UTILS,
    VCS,
    VIDEO,
    VIRTUAL,
    WEB,
    X11,
    XFCE,
    ZOPE

}