"""Render the saved measurements; does not run inference or invent missing samples."""
from pathlib import Path
import json
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import Patch

root=Path(__file__).parent
data=json.loads((root/'summary.json').read_text())
rows=data['rows']
fig,ax=plt.subplots(figsize=(14,9.2),dpi=160)
fig.subplots_adjust(left=.32,right=.97,top=.82,bottom=.20)
fig.suptitle('Gemma 4 E2B: CPU ISA and resident GPU forward',x=.06,y=.965,ha='left',fontsize=19,weight='bold')
fig.text(.06,.905,'23 prompt tokens + 128 generated tokens (fixed) | Median of 3 runs | Lower is better',fontsize=11)
colors=['#a7adb5' if row['prior'] else '#159578' if row['id'].startswith('resident-') else '#4d77ab' for row in rows]
for i,(row,color) in enumerate(zip(rows,colors)):
    value=row['medianSeconds']
    ax.barh(i,value,color=color,height=.62)
    ax.errorbar(value,i,xerr=[[value-row['minSeconds']],[row['maxSeconds']-value]],color='#29313c',capsize=3,linewidth=1)
    ax.text(max(value,row['maxSeconds'])+.8,i,f"{value:.2f} s  /  {row['percentOfPriorServer']:.1f}%",va='center',fontsize=10)
ax.set_yticks(range(len(rows)),[row['label'] for row in rows],fontsize=10.5)
ax.invert_yaxis();ax.set_xlim(0,86)
ax.set_xlabel('Prefill + decode time (seconds)',labelpad=10)
ax.xaxis.grid(True,alpha=.17);ax.set_axisbelow(True)
for spine in ['top','right','left']:ax.spines[spine].set_visible(False)
ax.tick_params(axis='y',length=0,pad=8)
ax.axvline(rows[0]['medianSeconds'],color='#8c929a',linestyle=':',linewidth=1)
fig.legend(handles=[Patch(color='#a7adb5',label='Previous measurement'),Patch(color='#4d77ab',label='Current measurement'),Patch(color='#159578',label='Resident GPU forward')],loc='lower left',bbox_to_anchor=(.055,.10),frameon=False,ncol=3,fontsize=10)
fig.text(.06,.075,'100% = previous Server AVX2 with SSD backing (53.64 s). Whiskers show the observed minimum and maximum.',fontsize=10)
fig.text(.06,.042,'Server: EPYC 9555 / 6 workers / 7 GB RSS budget. GPU host: Xeon E-2174G / 4 workers. Load and cleanup excluded.',fontsize=9.5,color='#424b55')
fig.savefig(root/'response-time.png');fig.savefig(root/'response-time.svg')
# Matplotlib leaves spaces at line endings inside SVG paths; normalize the text artifact.
svg=root/'response-time.svg'
svg.write_text('\n'.join(line.rstrip() for line in svg.read_text(encoding='utf-8').splitlines())+'\n',encoding='utf-8')
plt.close(fig)
