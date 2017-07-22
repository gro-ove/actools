#!/bin/zsh
zmodload zsh/datetime
setopt BRACE_CCL BSD_ECHO EXTENDED_GLOB MULTIBYTE NO_CASE_GLOB NO_CSH_JUNKIE_LOOPS NO_MATCH \
    NUMERIC_GLOB_SORT RC_EXPAND_PARAM RC_QUOTES RCS RE_MATCH_PCRE SHORT_LOOPS

function fxc(){
  '/cygdrive/C/Program Files (x86)/Windows Kits/8.1/bin/x64/fxc.exe' $@
}

for n in *.fx; do
  if [[ -e ${n%fx}ext ]]; then
    fxc /T ps_3_0 /O3 /E main /Fo${n%fx}ps $n || read _
  else
    fxc /T ps_2_0 /O3 /E main /Fo${n%fx}ps $n || read _
  fi
done
