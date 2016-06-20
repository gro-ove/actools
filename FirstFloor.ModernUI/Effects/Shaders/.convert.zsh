#!/bin/zsh
zmodload zsh/datetime
setopt BRACE_CCL BSD_ECHO EXTENDED_GLOB MULTIBYTE NO_CASE_GLOB NO_CSH_JUNKIE_LOOPS NO_MATCH \
    NUMERIC_GLOB_SORT RC_EXPAND_PARAM RC_QUOTES RCS RE_MATCH_PCRE SHORT_LOOPS

for n in *.fx; do
    fxc /T ps_2_0 /E main /Fo${n%fx}ps $n || read _
done
