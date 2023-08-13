var tomorrow = new Date("$TOMORROW$").getTime();
setInterval(function()
{
	var left = tomorrow - new Date().getTime();
	if (left > 0) document.getElementById("count").innerHTML = "Trial ends in " + getLeft(left);
	else if (left > -3600000)
	{
		left += 3600000;
		document.getElementById("count").innerHTML = "Trial finished! Final results in " + getLeft(left);
	}
	else document.getElementById("count").innerHTML = "Trial finished. <a href=\"$TODAY$.html\">Click here to view the final results.</a>";
}, 1000);
function getLeft(left)
{
	var s = Math.floor(left / 1000);
	var m = Math.floor(s / 60);
	var h = Math.floor(m / 60);
	s -= m * 60;
	m -= h * 60;
	return ("0" + h).slice(-2) + ":" + ("0" + m).slice(-2) + ":" + ("0" + s).slice(-2);
}
