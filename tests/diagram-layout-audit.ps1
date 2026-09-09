#Requires -Version 7.2
# Audit the complete authored corpus produced by --native --diagram-layout-only.
param([Parameter(Mandatory)][string]$Run, [ValidateRange(0, 1000)][int]$ExpectedGroups = 0)
$ErrorActionPreference='Stop'
# Independent readback audit: no layout library and no production comparator imports.
$records=Get-Content (Join-Path $Run 'native-diagram-layout.json') -Raw | ConvertFrom-Json
$applied=@($records | Where-Object {$_.tool -eq 'native_diagram_layout' -and $_.state.State -eq 'completed' -and $_.state.Result.plan.Direction -eq 'Down'})[-1].state.Result
$elements=@($applied.reopened.Elements)
$before=@(($records | Where-Object tool -eq 'native_presentation_apply')[0].state.Result.reopened.Elements)
if (-not $applied -or $before.Count -eq 0) { throw 'Missing complete native layout receipts; empty evidence cannot pass.' }
$issues=[Collections.Generic.List[string]]::new()
$nodes=0; $routes=0; $segments=0; $svgEndpoints=0
$anchors=0; $labels=0
# Independent enclosure checks use global native bounds, not solver receipts.
$groups=@($before|Where-Object Kind -eq Group)
if($groups.Count -ne $ExpectedGroups -or @($elements|Where-Object Kind -eq Group).Count -ne $ExpectedGroups){throw 'Incomplete group corpus'}
function ContainsBox($a,$b){return $b.X -ge $a.X-0.01 -and $b.Y -ge $a.Y-0.01 -and $b.X+$b.Width -le $a.X+$a.Width+0.01 -and $b.Y+$b.Height -le $a.Y+$a.Height+0.01}
function VisualBox($item){if($item.Geometry.Expanded -and $item.ExpandedGeometry){return $item.ExpandedGeometry}; return $item.Geometry}
function MemberMargins($box,$items){
  $bounds=@($items|ForEach-Object {VisualBox $_})
  $left=($bounds.X|Measure-Object -Minimum).Minimum; $top=($bounds.Y|Measure-Object -Minimum).Minimum
  $right=($bounds|ForEach-Object {$_.X+$_.Width}|Measure-Object -Maximum).Maximum
  $bottom=($bounds|ForEach-Object {$_.Y+$_.Height}|Measure-Object -Maximum).Maximum
  return @(($left-$box.X),($top-$box.Y),($box.X+$box.Width-$right),($box.Y+$box.Height-$bottom))
}
foreach($group in $groups){
  $next=@($elements|Where-Object Id -eq $group.Id)[0]
  $processes=@($before|Where-Object {$_.Kind -eq 'Process' -and $_.DiagramId -eq $group.DiagramId}|ForEach-Object Id)
  $oldAnchors=@($before|Where-Object {$_.DiagramId -eq $group.DiagramId -and (($_.Kind -eq 'Participant' -and $_.IsMainParticipant -eq $false) -or ($_.ParentId -in $processes -and $_.Kind -notin @('Lane','Milestone','SequenceFlow','MessageFlow','Association')))})
  $newAnchors=@($elements|Where-Object {$_.Id -in @($oldAnchors|ForEach-Object Id)})
  $oldMembers=@($oldAnchors|Where-Object {ContainsBox $group.Geometry (VisualBox $_)}|ForEach-Object Id|Sort-Object)
  $newMembers=@($newAnchors|Where-Object {ContainsBox $next.Geometry (VisualBox $_)}|ForEach-Object Id|Sort-Object)
  if(($oldMembers -join ',') -cne ($newMembers -join ',') -or -not $next.Geometry.Expanded -or $next.ParentId -ne $group.ParentId){$issues.Add('group-membership:'+$group.Id)}
  if($oldMembers.Count -gt 0){
    $oldMargins=MemberMargins $group.Geometry @($oldAnchors|Where-Object Id -in $oldMembers)
    $newMargins=MemberMargins $next.Geometry @($newAnchors|Where-Object Id -in $newMembers)
    for($m=0;$m -lt 4;$m++){if([math]::Abs($oldMargins[$m]-$newMargins[$m]) -gt 0.01){$issues.Add('group-margin:'+$group.Id)}}
  }elseif($group.Geometry.X -ne $next.Geometry.X -or $group.Geometry.Y -ne $next.Geometry.Y -or $group.Geometry.Width -ne $next.Geometry.Width -or $group.Geometry.Height -ne $next.Geometry.Height){$issues.Add('empty-group-geometry:'+$group.Id)}
  foreach($other in $groups|Where-Object Id -ne $group.Id){
    $afterOther=@($elements|Where-Object Id -eq $other.Id)[0]
    if((ContainsBox $group.Geometry $other.Geometry) -ne (ContainsBox $next.Geometry $afterOther.Geometry)){$issues.Add('group-nesting:'+$group.Id)}
  }
}
foreach($item in $elements){
  $old=@($before | Where-Object Id -eq $item.Id)[0]
  if($item.Kind -eq 'BoundaryEvent'){
    $anchors++
    if(($old.Event|ConvertTo-Json -Depth 20 -Compress) -cne ($item.Event|ConvertTo-Json -Depth 20 -Compress)){$issues.Add('boundary-semantics:'+$item.Id)}
    $hostNow=@($elements|Where-Object Id -eq $item.Event.AttachedToActivityId)[0].Geometry
    $hostOld=@($before|Where-Object Id -eq $old.Event.AttachedToActivityId)[0].Geometry
    if([math]::Abs(($item.Geometry.X-$hostNow.X)-($old.Geometry.X-$hostOld.X)) -gt 0.01 -or [math]::Abs(($item.Geometry.Y-$hostNow.Y)-($old.Geometry.Y-$hostOld.Y)) -gt 0.01){$issues.Add('boundary-offset:'+$item.Id)}
  }
  $label=$item.Style.LabelBounds; $oldLabel=$old.Style.LabelBounds
  if($oldLabel -and ($oldLabel.X -ne 0 -or $oldLabel.Y -ne 0 -or $oldLabel.Width -ne 0 -or $oldLabel.Height -ne 0)){
    $labels++
    if($label.Width -ne $oldLabel.Width -or $label.Height -ne $oldLabel.Height -or [math]::Abs(($label.X-$item.Geometry.X)-($oldLabel.X-$old.Geometry.X)) -gt 0.01 -or [math]::Abs(($label.Y-$item.Geometry.Y)-($oldLabel.Y-$old.Geometry.Y)) -gt 0.01){$issues.Add('manual-label:'+$item.Id)}
  }
}
$owners=@($elements | Where-Object {$_.Kind -in 'Process','Collaboration' -or $_.SubProcess})
foreach($owner in $owners){
  $parentIds=@($owner.Id)
  if($owner.Kind -eq 'Collaboration'){$parentIds=@($elements|Where-Object {$_.Kind -eq 'Process' -and $_.DiagramId -eq $owner.Id}|ForEach-Object Id)}
  $shapes=@($elements | Where-Object {$_.ParentId -in $parentIds -and $_.Kind -notin @('SequenceFlow','Association','MessageFlow','Lane','Milestone')} | ForEach-Object {
    [pscustomobject]@{Id=$_.Id; Host=$_.Event.AttachedToActivityId; Geometry=$(if($_.Geometry.Expanded -and $_.ExpandedGeometry){$_.ExpandedGeometry}else{$_.Geometry})}
  })
  $flows=@($elements | Where-Object {$_.ParentId -eq $owner.Id -and $_.Kind -in @('SequenceFlow','Association','MessageFlow')})
  $externalLabels=@($elements | Where-Object {$_.ParentId -in $parentIds -and $_.Style.LabelBounds.Width -gt 0 -and $_.Style.LabelBounds.Height -gt 0} | ForEach-Object {
    $l=$_.Style.LabelBounds; $g=$_.Geometry
    if(-not ($l.X -ge $g.X -and $l.Y -ge $g.Y -and $l.X+$l.Width -le $g.X+$g.Width -and $l.Y+$l.Height -le $g.Y+$g.Height)){
      [pscustomobject]@{Id=$_.Id; Geometry=$l}
    }
  })
  $headers=@()
  if($owner.Kind -eq 'Collaboration'){$headers=@($elements|Where-Object {$_.Kind -eq 'Participant' -and $_.IsMainParticipant -eq $false -and $_.DiagramId -eq $owner.Id}|ForEach-Object {[pscustomobject]@{Id=$_.Id; Geometry=[pscustomobject]@{X=$_.Geometry.X;Y=$_.Geometry.Y;Width=50;Height=$_.Geometry.Height}}})}
  if($owner.Kind -ne 'Collaboration'){$nodes+=$shapes.Count}; $routes+=$flows.Count
  # Expanded rectangles are real obstacles, not their smaller collapsed bounds.
  for($i=0;$i -lt $shapes.Count;$i++){for($j=$i+1;$j -lt $shapes.Count;$j++){
    # Only the explicit host/attached-event overlap is intentional.
    if($shapes[$i].Host -eq $shapes[$j].Id -or $shapes[$j].Host -eq $shapes[$i].Id){continue}
    $a=$shapes[$i].Geometry; $b=$shapes[$j].Geometry
    if($a.X -lt $b.X+$b.Width -and $b.X -lt $a.X+$a.Width -and $a.Y -lt $b.Y+$b.Height -and $b.Y -lt $a.Y+$a.Height){$issues.Add('node-overlap:'+$shapes[$i].Id+':'+$shapes[$j].Id)}
  }}
  foreach($flow in $flows){
    $source=$shapes | Where-Object Id -eq $flow.SourceId; $target=$shapes | Where-Object Id -eq $flow.TargetId
    $first=$flow.Points[0]; $last=$flow.Points[-1]
    $old=@($before | Where-Object Id -eq $flow.Id)[0]
    if($old.SourcePort -cne $flow.SourcePort -or $old.TargetPort -cne $flow.TargetPort){$issues.Add('port-metadata-changed:'+$flow.Id)}
    # Verify independently against the actual persisted midpoint IDs, not planner receipts.
    foreach($end in @(@{Shape=$source; Point=$first; Port=$flow.SourcePort},@{Shape=$target; Point=$last; Port=$flow.TargetPort})){
      $g=$end.Shape.Geometry
      $xy=switch($end.Port){ '1' {@(($g.X+$g.Width/2),$g.Y)} '2' {@(($g.X+$g.Width/2),($g.Y+$g.Height))} '3' {@($g.X,($g.Y+$g.Height/2))} '4' {@(($g.X+$g.Width),($g.Y+$g.Height/2))} default{throw 'Unsupported persisted port in audit'} }
      if([math]::Abs($end.Point.X-$xy[0]) -gt 0.01 -or [math]::Abs($end.Point.Y-$xy[1]) -gt 0.01){$issues.Add('endpoint-port:'+$flow.Id)}
    }
    for($i=1;$i -lt $flow.Points.Count;$i++){
      $a=$flow.Points[$i-1]; $b=$flow.Points[$i]; $segments++
      $vertical=[math]::Abs($a.X-$b.X) -lt 0.01; $horizontal=[math]::Abs($a.Y-$b.Y) -lt 0.01
      if(-not $vertical -and -not $horizontal){$issues.Add('diagonal:'+ $flow.Id)}
      foreach($shape in $shapes){
        $g=$shape.Geometry
        if($horizontal -and $a.Y -gt $g.Y+0.01 -and $a.Y -lt $g.Y+$g.Height-0.01 -and [math]::Min($a.X,$b.X) -lt $g.X+$g.Width-0.01 -and [math]::Max($a.X,$b.X) -gt $g.X+0.01){$issues.Add('interior-crossing:'+ $flow.Id+':'+$shape.Id)}
        if($vertical -and $a.X -gt $g.X+0.01 -and $a.X -lt $g.X+$g.Width-0.01 -and [math]::Min($a.Y,$b.Y) -lt $g.Y+$g.Height-0.01 -and [math]::Max($a.Y,$b.Y) -gt $g.Y+0.01){$issues.Add('interior-crossing:'+ $flow.Id+':'+$shape.Id)}
      }
      foreach($label in @($externalLabels)+@($headers)){
        $g=$label.Geometry
        if($horizontal -and $a.Y -gt $g.Y+0.01 -and $a.Y -lt $g.Y+$g.Height-0.01 -and [math]::Min($a.X,$b.X) -lt $g.X+$g.Width-0.01 -and [math]::Max($a.X,$b.X) -gt $g.X+0.01){$issues.Add('label-crossing:'+$flow.Id+':'+$label.Id)}
        if($vertical -and $a.X -gt $g.X+0.01 -and $a.X -lt $g.X+$g.Width-0.01 -and [math]::Min($a.Y,$b.Y) -lt $g.Y+$g.Height-0.01 -and [math]::Max($a.Y,$b.Y) -gt $g.Y+0.01){$issues.Add('label-crossing:'+$flow.Id+':'+$label.Id)}
      }
    }
  }
}
$renderedGroups=[Collections.Generic.HashSet[string]]::new()
foreach($render in $records | Where-Object tool -eq native_render_svg){
  $file=@($render.state.Result.result.Artifacts | Where-Object {$_ -like '*.svg'})[0]
  $settings=[Xml.XmlReaderSettings]::new(); $settings.DtdProcessing=[Xml.DtdProcessing]::Prohibit; $settings.XmlResolver=$null
  $reader=[Xml.XmlReader]::Create($file,$settings)
  try{$xml=[Xml.XmlDocument]::new(); $xml.Load($reader)}finally{$reader.Dispose()}
  foreach($shape in $xml.SelectNodes("//*[local-name()='g' and contains(@class,'djs-shape') and @data-element-id]")){
    $shapeId=$shape.GetAttribute('data-element-id')
    if($shapeId -in @($groups|ForEach-Object Id)){[void]$renderedGroups.Add($shapeId)}
  }
  foreach($group in $xml.SelectNodes("//*[local-name()='g' and contains(@class,'djs-connection') and @data-element-id]")){
    $id=$group.GetAttribute('data-element-id'); $flow=@($elements | Where-Object Id -eq $id)
    if($flow.Count -ne 1){throw 'SVG has an unknown or duplicate native connector identity'}
    $paths=@($group.SelectNodes("./*[local-name()='g' and @class='djs-visual']//*[local-name()='path' and not(ancestor::*[local-name()='defs'])]"))
    if($paths.Count -ne 1){throw 'SVG connection lacks one explicit visual path'}
    $path=$paths[0].GetAttribute('d')
    # Native rendering rounds corners with absolute cubic segments. Endpoint comparison
    # does not flatten or approximate those curves, and is not a full curve-clearance proof.
    if($path -match '[^MLC0-9.,+\-\s]'){throw ('Unexpected SVG route grammar, no approximate readback: '+$path)}
    foreach($command in [regex]::Matches($path,'([MLC])([^MLC]+)')){
      $count=[regex]::Matches($command.Groups[2].Value,'[-+]?(?:\d+\.?\d*|\.\d+)').Count
      if($count -ne $(if($command.Groups[1].Value -eq 'C'){6}else{2})){throw 'Invalid absolute SVG command arity'}
    }
    $numbers=@([regex]::Matches($path,'[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?') | ForEach-Object {[double]::Parse($_.Value,[Globalization.CultureInfo]::InvariantCulture)})
    if($numbers.Count -lt 4 -or $numbers.Count%2 -ne 0){throw 'Invalid SVG path coordinates'}
    $points=$flow[0].Points
    if([math]::Abs($numbers[0]-$points[0].X) -gt 0.02 -or [math]::Abs($numbers[1]-$points[0].Y) -gt 0.02 -or [math]::Abs($numbers[-2]-$points[-1].X) -gt 0.02 -or [math]::Abs($numbers[-1]-$points[-1].Y) -gt 0.02){$issues.Add('svg-endpoint:'+ $id)}
    $svgEndpoints++
  }
}
$artifact=$applied.outputArtifact
if($artifact -notmatch '^artifact:([0-9a-f]{32}):([^/\\]+)$'){throw 'Unrecognized completed native artifact'}
$nativeFile=Join-Path $Run "state/runs/$($Matches[1])/artifacts/$($Matches[2])"
$zip=[IO.Compression.ZipFile]::OpenRead($nativeFile); $copies=0
try{
  foreach($entry in @($zip.Entries|Where-Object FullName -like '*.diag')){
    $input=$entry.Open(); $bytes=[IO.MemoryStream]::new(); try{$input.CopyTo($bytes)}finally{$input.Dispose()}; $bytes.Position=0
    $nested=[IO.Compression.ZipArchive]::new($bytes,[IO.Compression.ZipArchiveMode]::Read)
    try{foreach($payload in @($nested.Entries|Where-Object FullName -eq 'Actions/Keep exact Ω.bin')){
      $stream=$payload.Open(); $value=[IO.MemoryStream]::new()
      try{$stream.CopyTo($value); if([Convert]::ToHexString($value.ToArray()) -cne '00FF131C000D'){throw 'Opaque bytes changed'}; $copies++}finally{$stream.Dispose();$value.Dispose()}
    }}finally{$nested.Dispose();$bytes.Dispose()}
  }
}finally{$zip.Dispose()}
if($copies -ne 1){throw 'Opaque payload occurrence count changed'}
if($renderedGroups.Count -ne $ExpectedGroups){throw 'Native SVG does not contain every expected group identity'}
# This is an acceptance-corpus assertion, not a production model-size restriction.
if($nodes -ne 19 -or $routes -ne 24 -or $anchors -ne 3 -or $labels -ne 17 -or $svgEndpoints -ne 28){throw 'Incomplete authored corpus; no empty or partial audit success.'}
$report=[ordered]@{nodes=$nodes; routes=$routes; segments=$segments; boundaryAnchors=$anchors; manualLabels=$labels; graphicalGroups=$groups.Count; renderedGroups=$renderedGroups.Count; svgEndpointPairs=$svgEndpoints; opaqueCopies=$copies; issues=@($issues); scope='Independent persisted geometry and SVG endpoint audit only; not complete rendered layout quality, desktop compatibility or full automation'}
$report | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $Run 'partitioned-independent-geometry.json')
$report | ConvertTo-Json -Depth 4
if($issues.Count -ne 0){throw 'Candidate geometry audit failed; preserve the original and inspect recorded identities'}
