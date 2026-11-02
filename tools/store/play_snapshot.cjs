// Read back this app only; temporary edit is deleted, never committed.
const fs=require('fs');
const {google}=require(process.env.GOOGLEAPIS_MODULE || 'googleapis');
(async()=>{
 const packageName='com.coddykit.borderlineempire';
 const auth=new google.auth.GoogleAuth({keyFile:process.env.GPLAY_SERVICE_ACCOUNT,scopes:['https://www.googleapis.com/auth/androidpublisher']});
 const api=google.androidpublisher({version:'v3',auth});
 const report={package:packageName,checked_at:new Date().toISOString(),scope:'API snapshot; Console declarations and device behavior are separate'};
 const editId=(await api.edits.insert({packageName})).data.id;
 try{
  report.tracks=(await api.edits.tracks.list({packageName,editId})).data.tracks;
  report.bundles=(await api.edits.bundles.list({packageName,editId})).data.bundles;
  report.details=(await api.edits.details.get({packageName,editId})).data;
  report.listings=(await api.edits.listings.list({packageName,editId})).data.listings;
  report.images={};
  for(const {language} of report.listings){
   report.images[language]={};
   for(const imageType of ['phoneScreenshots','sevenInchScreenshots','tenInchScreenshots','icon','featureGraphic']){
    const images=(await api.edits.images.list({packageName,editId,language,imageType})).data.images||[];
    report.images[language][imageType]=images.map(({id,sha256})=>({id,sha256}));
   }
  }
  try{await api.edits.validate({packageName,editId});report.edit_validation='passed (unchanged edit only)';}catch(e){report.edit_validation=e.message;}
 }finally{await api.edits.delete({packageName,editId});}
 const products=(await api.monetization.onetimeproducts.list({packageName})).data.oneTimeProducts||[];
 report.products=products.map(p=>({id:p.productId,locales:p.listings.map(l=>l.languageCode),options:p.purchaseOptions.map(o=>({id:o.purchaseOptionId,state:o.state,regions:o.regionalPricingAndAvailabilityConfigs.length}))}));
 fs.writeFileSync(process.argv[2],JSON.stringify(report,null,2)+'\n');
 console.log(JSON.stringify({tracks:report.tracks,locales:report.listings.length,products:report.products,validation:report.edit_validation}));
})().catch(e=>{console.error(e.message);process.exit(1)});
