#!/usr/bin/perl -w
use strict;
use Compress::Zlib;
use Cwd qw();
use Encode qw/encode decode/;
use File::Glob qw(:globally :nocase);

# This tool unzips packed step files 

#our $bytes="";

#our $fname=$ARGV[0];
our @files=<*.dat>;

foreach my $fname (@files)
{
	printf "unpacking $fname ";
	open FILE, $fname || die $!;
	binmode FILE;
	my $bytes="";
	my $count = read (FILE, $bytes, 169108570);
	close FILE;

	$fname=~s/.dat//; #remove extension
	printf "to $fname.step\n";
	if(substr($bytes,0,1) eq "\x78")
	{
		my $x = inflateInit();
		my $dest = $x->inflate($bytes);
		$fname .=".step";
		open OUT,">$fname";
		binmode OUT;
		print OUT $dest;
		close OUT;
	}
}
print "Done.\n";
sleep(1);

