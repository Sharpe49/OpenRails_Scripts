TCS_France
==========

EN :
This is a script for Open Rails that reproduces the behaviour of the French Train Control Systems.
The systems that will be available are :
- RSO : Répétition des Signaux Optique (Optical Signal Repetition)
- DAAT : Dispositif d'Arrêt Automatique des Trains (Automatic Train Stop System)
- KVB : Contrôle de vitesse par balises (Beacon-based Speed Control)
- TVM 300 and 430 : Transmission Voie Machine (Track to Machine Transmission)
- ETCS : European Train Control System

To install it, put the CS file and the INI files in the Script folder of your locomotive (create it if needed).
Put the SMS file and the WAV files in the Sound folder of your locomotive.
Using the existing INI files, you can write your own parameters file if your train is different.
In your ENG file, in the Engine section, write these parameters :
- ORTSTrainControlSystem( TCS_France.cs )
- ORTSTrainControlSystemSound( TCS_France.sms )
- ORTSTrainControlSystemParameters( <name of your INI file> )
The TCS should work now.

Advice : In order to have a better ergonomy with the alerter, swap the SPACE and Z keys.

FR :
Ceci est un script pour Open Rails permettant de reproduire le comportement des systèmes de sécurité des train français.
Les systèmes qui seront disponibles sont :
- RSO : Répétition des Signaux Optique
- DAAT : Dispositif d'Arrêt Automatique des Trains
- KVB : Contrôle de vitesse par balises
- TVM 300 and 430 : Transmission Voie Machine
- ETCS : European Train Control System (Système Européen de Contrôle des Trains)

Pour l'installer, mettez le fichier CS et les fichiers INI dans le dossier Script de votre locomotive (créez le si nécessaire).
Mettez le fichier SMS et les fichiers WAV dans le dossier Sound de votre locomotive.
En utilisant les fichiers INI existants, vous pouvez créer votre propre fichier de paramètres si votre train est différent.
Dans votre fichier ENG, dans la section Engine, écrivez ces paramètres :
- ORTSTrainControlSystem( TCS_France.cs )
- ORTSTrainControlSystemSound( TCS_France.sms )
- ORTSTrainControlSystemParameters( <nom de votre fichier INI> )
Le système de contrôle du train devrait fonctionner désormais.

Conseil : Pour avoir une meilleure ergonomie avec la veille automatique, intervertissez les touches ESPACE et W.
