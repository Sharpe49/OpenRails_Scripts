Open Rails Scripts
==================

EN : In order to copy parameters correctly, please open this file on your computer (do not copy parameters from the Github website).
FR : Afin de copier les paramètres de manière correcte, veuillez ouvrir ce fichier sur votre ordinateur (ne pas copier les paramètres depuis le site Github).

## TCS_France

EN :
This is a script for Open Rails that reproduces the behaviour of the French Train Control Systems.
The systems that will be available are :
- RSO : Répétition des Signaux Optique (Optical Signal Repetition)
- DAAT : Dispositif d'Arrêt Automatique des Trains (Automatic Train Stop System)
- KVB : Contrôle de vitesse par balises (Beacon-based Speed Control)
- TVM 300 and 430 : Transmission Voie Machine (Track to Machine Transmission)
- ETCS : European Train Control System (not available for now)

If you need different sounds, put the SMS file and the WAV files in the Sound folder of your locomotive.
Using the existing INI files, you can write your own parameters file if your train is different.
To activate the script, in your ENG file, in the Engine section, write these parameters:  
ORTSTrainControlSystem( "..\\..\\common.script\\TCS_France.cs" )  
ORTSTrainControlSystemSound( "..\\..\\common.sound\\TCS_France\\TCS_France.sms" )  
ORTSTrainControlSystemParameters( "..\\..\\common.script\\< name of your INI file >" )  

Advice : In order to have a better ergonomy with the alerter, swap the SPACE and Z keys.
For the acknowledge button (Control Generic 1), assign the key Q (Doors Left by default). You can assign Ctrl+A for the opening of the doors on the left.

FR :
Ce script pour Open Rails reproduit le comportement des systèmes de sécurité des trains français.
Les systèmes qui seront disponibles sont :
- RSO : Répétition des Signaux Optique
- DAAT : Dispositif d'Arrêt Automatique des Trains
- KVB : Contrôle de vitesse par balises
- TVM 300 and 430 : Transmission Voie Machine
- ETCS : European Train Control System (non disponible pour le moment)

Si vous avez besoin de sons différents, mettez le fichier SMS et les fichiers WAV dans le dossier Sound de votre locomotive.
En utilisant les fichiers INI existants, vous pouvez créer votre propre fichier de paramètres si votre train est différent.
Pour activer le script, dans votre fichier ENG, dans la section Engine, écrivez ces paramètres :  
ORTSTrainControlSystem( "..\\..\\common.script\\TCS_France.cs" )  
ORTSTrainControlSystemSound( "..\\..\\common.sound\\TCS_France\\TCS_France.sms" )  
ORTSTrainControlSystemParameters( "..\\..\\common.script\\< name of your INI file >" )  

Conseil : Pour avoir une meilleure ergonomie avec la veille automatique, intervertissez les touches ESPACE (Sifflet par défaut) et W (Veille automatique par défaut).
De même, pour la touche d'acquittement (Contrôle Générique 1), assignez la touche A (Portes Gauche par défaut). Vous pouvez assigner Ctrl+A pour l'ouverture des portes à gauche.

## Old_TCS_France

EN :
This is a script for Open Rails that reproduces the behaviour of the French Train Control Systems for older trains (without KVB).
The systems that will be available are :
- RS : Répétition des Signaux (Signal Repetition)
- DAAT : Dispositif d'Arrêt Automatique des Trains (Automatic Train Stop System)
- TVM 300 : Transmission Voie Machine (Track to Machine Transmission)

If you need different sounds, put the SMS file and the WAV files in the Sound folder of your locomotive.
Using the existing INI files, you can write your own parameters file if your train is different.
To activate the script, in your ENG file, in the Engine section, write these parameters:  
ORTSTrainControlSystem( "..\\..\\common.script\\Old_TCS_France.cs" )  
ORTSTrainControlSystemSound( "..\\..\\common.sound\\TCS_France\\Old_TCS_France.sms" )  
ORTSTrainControlSystemParameters( "..\\..\\common.script\\< name of your INI file >" )  

Advice : In order to have a better ergonomy with the alerter, swap the SPACE and Z keys.
For the acknowledge button (Control Generic 1), assign the key Q (Doors Left by default). You can assign Ctrl+A for the opening of the doors on the left.

FR :
Ce script pour Open Rails reproduit le comportement des systèmes de sécurité des trains français plus anciens (notamment sans KVB).
Les systèmes qui seront disponibles sont :
- RS : Répétition des Signaux
- DAAT : Dispositif d'Arrêt Automatique des Trains
- TVM 300 : Transmission Voie Machine

Si vous avez besoin de sons différents, mettez le fichier SMS et les fichiers WAV dans le dossier Sound de votre locomotive.
En utilisant les fichiers INI existants, vous pouvez créer votre propre fichier de paramètres si votre train est différent.
Pour activer le script, dans votre fichier ENG, dans la section Engine, écrivez ces paramètres :  
ORTSTrainControlSystem( "..\\..\\common.script\\Old_TCS_France.cs" )  
ORTSTrainControlSystemSound( "..\\..\\common.sound\\TCS_France\\Old_TCS_France.sms" )  
ORTSTrainControlSystemParameters( "..\\..\\common.script\\< name of your INI file >" )  

Conseil : Pour avoir une meilleure ergonomie avec la veille automatique, intervertissez les touches ESPACE et W.
De même, pour la touche d'acquittement (Contrôle Générique 1), assignez la touche A (Portes Gauche par défaut). Vous pouvez assigner Ctrl+A pour l'ouverture des portes à gauche.

## PBL2BrakeController

EN :
This script reproduces the behaviour of the PBL2 brake controller used in French locomotives.
To activate the script, in your ENG file, in the Engine section, write this parameter:  
ORTSTrainBrakeController( "..\\..\\common.script\\PBL2BrakeController.cs" )  

FR :
Ce script pour Open Rails reproduit le comportement du robinet de freinage PBL2 utilisé dans les locomotives françaises.
Pour activer le script, dans votre fichier ENG, dans la section Engine, écrivez ce paramètre :  
ORTSTrainBrakeController( "..\\..\\common.script\\PBL2BrakeController.cs" )  

## PBA2BrakeController

EN :
This script reproduces the behaviour of the PBA2 brake controller used in French multiple units.
To activate the script, in your ENG file, in the Engine section, write this parameter:  
ORTSTrainBrakeController( "..\\..\\common.script\\PBA2BrakeController.cs" )  

FR :
Ce script pour Open Rails reproduit le comportement du robinet de freinage PBA2 utilisé dans les automotrices françaises.
Pour activer le script, dans votre fichier ENG, dans la section Engine, écrivez ce paramètre :  
ORTSTrainBrakeController( "..\\..\\common.script\\PBA2BrakeController.cs" )  

## SNCFCircuitBreaker

EN : This script reproduces the behaviour of the circuit breaker present in modern SNCF locomotives.
To activate the script, in your ENG file, in the Engine section, write this parameter:  
ORTSCircuitBreaker( "..\\..\\common.script\\SNCFCircuitBreaker.cs" )  

The default key assignments for the circuit breaker are:
- O for the circuit breaker closing order button BP(DJ)
- Shift+O for the circuit breaker closing authrorization switch Z(DJ)

FR :
Ce script pour Open Rails reproduit le comportement du disjoncteur présent dans les locomotives SNCF modernes.
Pour activer le script, dans votre fichier ENG, dans la section Engine, écrivez ce paramètre :  
ORTSCircuitBreaker( "..\\..\\common.script\\SNCFCircuitBreaker.cs" )  

Les touches pour contrôler le disjoncteur sont, par défaut :
- O pour le bouton de fermeture du disjonteur BP(DJ)
- Shift+O pour l'interrupteur d'autorisation de fermeture du disjoncteur Z(DJ)
